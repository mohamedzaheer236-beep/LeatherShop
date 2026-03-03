using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Product;
using LeatherShopAPI.Models;
using Asp.Versioning;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiVersion("1.0")]
[Authorize]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? brand,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var result = await _productService.GetAllAsync(category, brand, search, page, pageSize, ct);
        return Ok(ApiResponse<PaginatedResult<ProductDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        if (product == null)
            return NotFound(ApiResponse<ProductDto>.Fail("Product not found."));
        return Ok(ApiResponse<ProductDto>.Ok(product));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto, CancellationToken ct)
    {
        var product = await _productService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id },
            ApiResponse<ProductDto>.Ok(product, "Product created successfully."));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductDto dto, CancellationToken ct)
    {
        var success = await _productService.UpdateAsync(id, dto, ct);
        if (!success)
            return NotFound(ApiResponse.Fail("Product not found."));
        return Ok(ApiResponse.Ok("Product updated successfully."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var success = await _productService.DeleteAsync(id, ct);
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
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var categories = await _productService.GetCategoriesAsync(ct);
        return Ok(ApiResponse<List<string>>.Ok(categories));
    }

    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands(CancellationToken ct)
    {
        var brands = await _productService.GetBrandsAsync(ct);
        return Ok(ApiResponse<List<string>>.Ok(brands));
    }

    [HttpGet("check-name")]
    public async Task<IActionResult> CheckName([FromQuery] string name, [FromQuery] int? excludeId, CancellationToken ct)
    {
        var exists = await _productService.NameExistsAsync(name, excludeId, ct);
        return Ok(ApiResponse<bool>.Ok(exists));
    }

    [HttpPost("upload-image")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file provided."));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("File size must be under 5 MB."));

        try
        {
            var relativePath = await _productService.UploadImageAsync(file, ct);
            return Ok(ApiResponse<string>.Ok(relativePath, "Image uploaded successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("upload-images")]
    [RequestSizeLimit(25 * 1024 * 1024)] // 25 MB total (5 files × 5 MB)
    public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files, CancellationToken ct)
    {
        if (files == null || files.Count == 0)
            return BadRequest(ApiResponse.Fail("No files provided."));

        const int MaxFiles = 4;
        if (files.Count > MaxFiles)
            return BadRequest(ApiResponse.Fail($"Maximum {MaxFiles} files per upload."));

        if (files.Any(f => f.Length > 5 * 1024 * 1024))
            return BadRequest(ApiResponse.Fail("Each file must be under 5 MB."));

        try
        {
            var relativePaths = await _productService.UploadImagesAsync(files, ct);
            return Ok(ApiResponse<List<string>>.Ok(relativePaths, "Images uploaded successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}
