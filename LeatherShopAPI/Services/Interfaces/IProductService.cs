using LeatherShopAPI.DTOs.Product;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface IProductService
{
    Task<PaginatedResult<ProductDto>> GetAllAsync(string? category, string? brand, string? search, int page = 1, int pageSize = 25);
    Task<ProductDto?> GetByIdAsync(int id);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<bool> UpdateAsync(int id, UpdateProductDto dto);
    Task<bool> DeleteAsync(int id);
    Task<List<string>> GetCategoriesAsync();
    Task<List<string>> GetBrandsAsync();
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
    Task<string> UploadImageAsync(IFormFile file);
    Task<List<string>> UploadImagesAsync(IList<IFormFile> files);
}
