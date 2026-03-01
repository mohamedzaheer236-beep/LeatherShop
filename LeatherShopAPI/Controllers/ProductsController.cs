using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Product;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category, [FromQuery] string? brand, [FromQuery] string? search)
    {
        var products = await _productService.GetAllAsync(category, brand, search);
        return Ok(ApiResponse<List<ProductDto>>.Ok(products));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null)
            return NotFound(ApiResponse<ProductDto>.Fail("Product not found."));
        return Ok(ApiResponse<ProductDto>.Ok(product));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        var product = await _productService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = product.Id },
            ApiResponse<ProductDto>.Ok(product, "Product created successfully."));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductDto dto)
    {
        var success = await _productService.UpdateAsync(id, dto);
        if (!success)
            return NotFound(ApiResponse.Fail("Product not found."));
        return Ok(ApiResponse.Ok("Product updated successfully."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var success = await _productService.DeleteAsync(id);
            if (!success)
                return NotFound(ApiResponse.Fail("Product not found."));
            return Ok(ApiResponse.Ok("Product deleted successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _productService.GetCategoriesAsync();
        return Ok(ApiResponse<List<string>>.Ok(categories));
    }

    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands()
    {
        var brands = await _productService.GetBrandsAsync();
        return Ok(ApiResponse<List<string>>.Ok(brands));
    }

    [HttpGet("check-name")]
    public async Task<IActionResult> CheckName([FromQuery] string name, [FromQuery] int? excludeId)
    {
        var exists = await _productService.NameExistsAsync(name, excludeId);
        return Ok(ApiResponse<bool>.Ok(exists));
    }

    [HttpPost("upload-image")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file provided."));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("File size must be under 5 MB."));

        try
        {
            var relativePath = await _productService.UploadImageAsync(file);
            return Ok(ApiResponse<string>.Ok(relativePath, "Image uploaded successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("upload-images")]
    [RequestSizeLimit(25 * 1024 * 1024)] // 25 MB total (5 files × 5 MB)
    public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest(ApiResponse.Fail("No files provided."));

        const int MaxFiles = 10;
        if (files.Count > MaxFiles)
            return BadRequest(ApiResponse.Fail($"Maximum {MaxFiles} files per upload."));

        if (files.Any(f => f.Length > 5 * 1024 * 1024))
            return BadRequest(ApiResponse.Fail("Each file must be under 5 MB."));

        try
        {
            var relativePaths = await _productService.UploadImagesAsync(files);
            return Ok(ApiResponse<List<string>>.Ok(relativePaths, "Images uploaded successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}
