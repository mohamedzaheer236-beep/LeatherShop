using LeatherShopAPI.DTOs.Order;

namespace LeatherShopAPI.Services.Interfaces;

public interface IOrderService
{
    Task<List<OrderDto>> GetAllAsync(string? status);
    Task<bool> UpdateStatusAsync(int id, string newStatus);
}
