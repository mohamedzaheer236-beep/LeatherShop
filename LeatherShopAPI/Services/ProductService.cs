using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Product;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;

    public ProductService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProductDto>> GetAllAsync(string? category, string? brand, string? search)
    {
        var query = _db.Products.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category.ToLower() == category.ToLower());

        if (!string.IsNullOrEmpty(brand))
            query = query.Where(p => p.Brand.ToLower() == brand.ToLower());

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()) || p.Description.ToLower().Contains(search.ToLower()));

        return await query.OrderByDescending(p => p.CreatedAt).Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Brand = p.Brand,
            Category = p.Category,
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive
        }).ToListAsync();
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return null;

        return new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Brand = p.Brand,
            Category = p.Category,
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive
        };
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
            ImageUrl = dto.ImageUrl
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Brand = product.Brand,
            Category = product.Category,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            ImageUrl = product.ImageUrl,
            IsActive = product.IsActive
        };
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return false;

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.Description != null) product.Description = dto.Description;
        if (dto.Brand != null) product.Brand = dto.Brand;
        if (dto.Category != null) product.Category = dto.Category;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.StockQuantity.HasValue) product.StockQuantity = dto.StockQuantity.Value;
        if (dto.ImageUrl != null) product.ImageUrl = dto.ImageUrl;
        if (dto.IsActive.HasValue) product.IsActive = dto.IsActive.Value;

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
}
