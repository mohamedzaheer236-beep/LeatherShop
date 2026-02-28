using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Product;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LeatherShopAPI.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProductService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<List<ProductDto>> GetAllAsync(string? category, string? brand, string? search)
    {
        var query = _db.Products.Include(p => p.Images).AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category.ToLower() == category.ToLower());

        if (!string.IsNullOrEmpty(brand))
            query = query.Where(p => p.Brand.ToLower() == brand.ToLower());

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()) || p.Description.ToLower().Contains(search.ToLower()));

        var products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return products.Select(p => p.ToDto()).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        var p = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return null;

        return p.ToDto();
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Brand = dto.Brand,
            Category = dto.Category,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            ImageUrl = dto.ImageUrl ?? string.Empty
        };

        // Add additional images if provided
        if (dto.ImageUrls is { Count: > 0 })
        {
            for (int i = 0; i < dto.ImageUrls.Count; i++)
            {
                product.Images.Add(new ProductImage
                {
                    ImageUrl = dto.ImageUrls[i],
                    DisplayOrder = i
                });
            }
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return product.ToDto();
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductDto dto)
    {
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return false;

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.Description != null) product.Description = dto.Description;
        if (dto.Brand != null) product.Brand = dto.Brand;
        if (dto.Category != null) product.Category = dto.Category;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.StockQuantity.HasValue) product.StockQuantity = dto.StockQuantity.Value;
        if (dto.ImageUrl != null) product.ImageUrl = dto.ImageUrl;
        if (dto.IsActive.HasValue) product.IsActive = dto.IsActive.Value;

        // Replace additional images if the list was explicitly provided
        if (dto.ImageUrls != null)
        {
            // Remove existing additional images
            _db.ProductImages.RemoveRange(product.Images);

            // Add new additional images
            for (int i = 0; i < dto.ImageUrls.Count; i++)
            {
                product.Images.Add(new ProductImage
                {
                    ImageUrl = dto.ImageUrls[i],
                    DisplayOrder = i
                });
            }
        }

        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return false;

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        return await _db.Products
            .Where(p => p.IsActive)
            .Select(p => p.Category)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<string>> GetBrandsAsync()
    {
        return await _db.Products
            .Where(p => p.IsActive)
            .Select(p => p.Brand)
            .Distinct()
            .ToListAsync();
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        var query = _db.Products.Where(p => p.Name.ToLower() == name.ToLower());
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLower();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        if (!allowedExts.Contains(ext))
            throw new ArgumentException("Only image files (.jpg, .png, .webp, .gif) are allowed.");

        // All images are resized + compressed to ~300KB JPEG
        // This ensures WhatsApp carousel compatibility and saves storage
        var fileName = $"{Guid.NewGuid()}.jpg";
        var filePath = Path.Combine(uploadsDir, fileName);

        using var inputStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(inputStream);

        // Resize if larger than 1200px on either dimension
        const int maxDimension = 1200;
        if (image.Width > maxDimension || image.Height > maxDimension)
        {
            var options = new SixLabors.ImageSharp.Processing.ResizeOptions
            {
                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max,
                Size = new SixLabors.ImageSharp.Size(maxDimension, maxDimension)
            };
            image.Mutate(x => x.Resize(options));
        }

        // Iteratively lower quality to target ~300KB
        const int targetBytes = 300 * 1024;
        int quality = 85;
        while (quality >= 30)
        {
            using var testStream = new MemoryStream();
            await image.SaveAsJpegAsync(testStream, new JpegEncoder { Quality = quality });
            if (testStream.Length <= targetBytes || quality <= 30)
            {
                await System.IO.File.WriteAllBytesAsync(filePath, testStream.ToArray());
                break;
            }
            quality -= 10;
        }

        return $"/uploads/{fileName}";
    }

    public async Task<List<string>> UploadImagesAsync(IList<IFormFile> files)
    {
        if (files.Count > 4)
            throw new ArgumentException("Maximum 4 images allowed per product.");

        var paths = new List<string>();
        foreach (var file in files)
        {
            var path = await UploadImageAsync(file);
            paths.Add(path);
        }
        return paths;
    }
}
