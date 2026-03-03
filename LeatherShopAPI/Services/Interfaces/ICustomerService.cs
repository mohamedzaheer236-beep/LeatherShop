using LeatherShopAPI.DTOs.Customer;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface ICustomerService
{
    Task<PaginatedResult<CustomerListDto>> GetAllAsync(bool? subscribedOnly, string? search, int page = 1, int pageSize = 25);
    Task<CustomerCountDto> GetCountAsync();
    Task<CustomerCreatedDto> CreateAsync(CreateCustomerDto dto);
    Task<CustomerListDto?> UpdateAsync(int id, UpdateCustomerDto dto);
    Task<DeleteCustomerResponse> DeleteAsync(int id);
    Task<BulkImportResultDto> BulkImportAsync(BulkImportDto dto);
    Task<bool> ToggleSubscriptionAsync(int id, bool isSubscribed);
}
