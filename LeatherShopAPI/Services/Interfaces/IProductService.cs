using System.Threading;
using LeatherShopAPI.DTOs.Product;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface IProductService
{
    Task<PaginatedResult<ProductDto>> GetAllAsync(string? category, string? brand, string? search, int page = 1, int pageSize = 25,
        string? sortField = null, string? sortOrder = null, string? name = null,
        decimal? priceMin = null, decimal? priceMax = null, int? stockMin = null, int? stockMax = null,
        string? isActive = null, string? dateFrom = null, string? dateTo = null, CancellationToken ct = default);
    Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateProductDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);
    Task<List<string>> GetBrandsAsync(CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default);
    Task<string> UploadImageAsync(IFormFile file, CancellationToken ct = default);
    Task<List<string>> UploadImagesAsync(IList<IFormFile> files, CancellationToken ct = default);
    Task<string> UploadVideoAsync(IFormFile file, CancellationToken ct = default);
}
