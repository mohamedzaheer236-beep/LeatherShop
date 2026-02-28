using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface IOrderService
{
    Task<PaginatedResult<OrderDto>> GetAllAsync(string? status, int page = 1, int pageSize = 25);
    Task<Order?> GetByIdWithDetailsAsync(int id);
    Task<bool> UpdateStatusAsync(int id, string newStatus);
}
