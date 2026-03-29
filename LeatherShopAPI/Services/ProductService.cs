using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Product;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;
using static LeatherShopAPI.Extensions.SqlHelper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LeatherShopAPI.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProductService> _logger;

    public ProductService(AppDbContext db, IWebHostEnvironment env, ILogger<ProductService> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    public async Task<PaginatedResult<ProductDto>> GetAllAsync(string? category, string? brand, string? search, int page = 1, int pageSize = 25,
        string? sortField = null, string? sortOrder = null, string? name = null,
        decimal? priceMin = null, decimal? priceMax = null, int? stockMin = null, int? stockMax = null,
        string? isActive = null, string? dateFrom = null, string? dateTo = null, CancellationToken ct = default)
    {
        var baseQuery = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(category))
            baseQuery = baseQuery.Where(p => EF.Functions.ILike(p.Category, EscapeLikePattern(category)));

        if (!string.IsNullOrEmpty(brand))
            baseQuery = baseQuery.Where(p => EF.Functions.ILike(p.Brand, EscapeLikePattern(brand)));

        if (!string.IsNullOrEmpty(search))
        {
            var escaped = EscapeLikePattern(search);
            baseQuery = baseQuery.Where(p => EF.Functions.ILike(p.Name, $"%{escaped}%") || EF.Functions.ILike(p.Description, $"%{escaped}%"));
        }

        // Column filters
        if (!string.IsNullOrWhiteSpace(name))
        {
            var escaped = EscapeLikePattern(name.Trim());
            baseQuery = baseQuery.Where(p => EF.Functions.ILike(p.Name, $"%{escaped}%"));
        }

        if (priceMin.HasValue)
            baseQuery = baseQuery.Where(p => p.Price >= priceMin.Value);
        if (priceMax.HasValue)
            baseQuery = baseQuery.Where(p => p.Price <= priceMax.Value);

        if (stockMin.HasValue)
            baseQuery = baseQuery.Where(p => p.StockQuantity >= stockMin.Value);
        if (stockMax.HasValue)
            baseQuery = baseQuery.Where(p => p.StockQuantity <= stockMax.Value);

        if (!string.IsNullOrWhiteSpace(isActive))
        {
            if (bool.TryParse(isActive, out var activeVal))
                baseQuery = baseQuery.Where(p => p.IsActive == activeVal);
        }

        if (!string.IsNullOrWhiteSpace(dateFrom) && DateOnly.TryParse(dateFrom, out var fromDate))
        {
            var start = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            baseQuery = baseQuery.Where(p => p.CreatedAt >= start);
        }

        if (!string.IsNullOrWhiteSpace(dateTo) && DateOnly.TryParse(dateTo, out var toDate))
        {
            var end = toDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
            baseQuery = baseQuery.Where(p => p.CreatedAt < end);
        }

        var totalCount = await baseQuery.CountAsync(ct);

        // Sorting
        var isDesc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        IQueryable<Product> sorted = (sortField?.ToLower()) switch
        {
            "name" => isDesc ? baseQuery.OrderByDescending(p => p.Name) : baseQuery.OrderBy(p => p.Name),
            "brand" => isDesc ? baseQuery.OrderByDescending(p => p.Brand) : baseQuery.OrderBy(p => p.Brand),
            "category" => isDesc ? baseQuery.OrderByDescending(p => p.Category) : baseQuery.OrderBy(p => p.Category),
            "price" => isDesc ? baseQuery.OrderByDescending(p => p.Price) : baseQuery.OrderBy(p => p.Price),
            "stockquantity" => isDesc ? baseQuery.OrderByDescending(p => p.StockQuantity) : baseQuery.OrderBy(p => p.StockQuantity),
            "isactive" => isDesc ? baseQuery.OrderByDescending(p => p.IsActive) : baseQuery.OrderBy(p => p.IsActive),
            _ => isDesc ? baseQuery.OrderByDescending(p => p.CreatedAt) : baseQuery.OrderBy(p => p.CreatedAt),
        };

        var products = await sorted.Include(p => p.Images)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<ProductDto>
        {
            Items = products.Select(p => p.ToDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var p = await _db.Products.AsNoTracking().Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (p == null) return null;

        return p.ToDto();
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Brand = dto.Brand,
            Category = dto.Category,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            ImageUrl = dto.ImageUrl ?? string.Empty,
            VideoUrl = dto.VideoUrl
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
        await _db.SaveChangesAsync(ct);

        return product.ToDto();
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductDto dto, CancellationToken ct = default)
    {
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return false;

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.Description != null) product.Description = dto.Description;
        if (dto.Brand != null) product.Brand = dto.Brand;
        if (dto.Category != null) product.Category = dto.Category;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.StockQuantity.HasValue) product.StockQuantity = dto.StockQuantity.Value;
        if (dto.ImageUrl != null) product.ImageUrl = dto.ImageUrl;
        if (dto.VideoUrl != null) product.VideoUrl = dto.VideoUrl.Length == 0 ? null : dto.VideoUrl;
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

            // If imageUrls was sent as empty AND imageUrl is null, clear the primary image too
            if (dto.ImageUrls.Count == 0 && dto.ImageUrl == null)
            {
                product.ImageUrl = string.Empty;
            }
        }

        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync(new object[] { id }, ct);
        if (product == null) return false;

        // Check if product has been ordered - can't delete products with order history
        var hasOrders = await _db.OrderItems.AnyAsync(oi => oi.ProductId == id, ct);
        if (hasOrders)
            throw new InvalidOperationException("Cannot delete a product that has been ordered. Deactivate it instead.");

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static readonly List<string> DefaultCategories =
    [
        "Men's Bags", "Women's Handbags", "Backpacks", "Duffel & Travel Bags",
        "Wallets", "Card Holders", "Belts", "Desk Mats & Office",
        "Accessories", "Corporate Gifts"
    ];

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var dbCategories = await _db.Products
            .Select(p => p.Category)
            .Distinct()
            .ToListAsync(ct);

        return DefaultCategories
            .Union(dbCategories, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }

    public async Task<List<string>> GetBrandsAsync(CancellationToken ct = default)
    {
        return await _db.Products
            .Where(p => p.IsActive)
            .Select(p => p.Brand)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default)
    {
        var escaped = EscapeLikePattern(name);
        var query = _db.Products.Where(p => EF.Functions.ILike(p.Name, escaped));
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task<string> UploadImageAsync(IFormFile file, CancellationToken ct = default)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        if (!allowedExts.Contains(ext))
            throw new ArgumentException("Only image files (.jpg, .png, .webp, .gif) are allowed.");

        // Guard against extremely large files before loading into memory
        if (file.Length > 20 * 1024 * 1024)
            throw new ArgumentException("Image file is too large. Please upload a smaller file.");

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

    public async Task<List<string>> UploadImagesAsync(IList<IFormFile> files, CancellationToken ct = default)
    {
        if (files.Count > 4)
            throw new ArgumentException("Maximum 4 images allowed per product.");

        var paths = new List<string>();
        foreach (var file in files)
        {
            var path = await UploadImageAsync(file, ct);
            paths.Add(path);
        }
        return paths;
    }

    public async Task<string> UploadVideoAsync(IFormFile file, CancellationToken ct = default)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".mp4", ".3gp" };
        if (!allowedExts.Contains(ext))
            throw new ArgumentException("Only video files (.mp4, .3gp) are allowed.");

        if (file.Length > 16 * 1024 * 1024)
            throw new ArgumentException("Video file must be under 16 MB (WhatsApp limit).");

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        try
        {
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video upload failed for file {FileName}, cleaning up partial file", fileName);
            if (File.Exists(filePath)) File.Delete(filePath);
            throw;
        }

        return $"/uploads/{fileName}";
    }
}
