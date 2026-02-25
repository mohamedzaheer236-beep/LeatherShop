using LeatherShopAPI.DTOs.Customer;

namespace LeatherShopAPI.Services.Interfaces;

public interface ICustomerService
{
    Task<List<CustomerListDto>> GetAllAsync(bool? subscribedOnly, string? search);
    Task<CustomerCountDto> GetCountAsync();
    Task<CustomerCreatedDto> CreateAsync(CreateCustomerDto dto);
    Task<CustomerListDto?> UpdateAsync(int id, UpdateCustomerDto dto);
    Task<DeleteCustomerResponse> DeleteAsync(int id);
    Task<BulkImportResultDto> BulkImportAsync(BulkImportDto dto);
    Task<bool> ToggleSubscriptionAsync(int id, bool isSubscribed);
}
