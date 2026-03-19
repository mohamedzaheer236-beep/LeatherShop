using System.Threading;
using LeatherShopAPI.DTOs.Customer;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface ICustomerService
{
    Task<PaginatedResult<CustomerListDto>> GetAllAsync(bool? subscribedOnly, string? search, string? category, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task<CustomerCountDto> GetCountAsync(CancellationToken ct = default);
    Task<CustomerCreatedDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default);
    Task<CustomerListDto?> UpdateAsync(int id, UpdateCustomerDto dto, CancellationToken ct = default);
    Task<DeleteCustomerResponse> DeleteAsync(int id, CancellationToken ct = default);
    Task<BulkImportResultDto> BulkImportAsync(BulkImportDto dto, CancellationToken ct = default);
    Task<bool> ToggleSubscriptionAsync(int id, bool isSubscribed, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);
    Task<List<string>> CheckPhonesAsync(List<string> phones, CancellationToken ct = default);
    Task<BulkDeleteResultDto> BulkDeleteAsync(List<int> ids, CancellationToken ct = default);
}
