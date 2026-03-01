using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

/// <summary>Result of an order status update operation.</summary>
public enum UpdateStatusResult
{
    Success,
    NotFound,
    InvalidStatus,
    InvalidTransition
}

public interface IOrderService
{
    Task<PaginatedResult<OrderDto>> GetAllAsync(string? status, int page = 1, int pageSize = 25);
    Task<Order?> GetByIdWithDetailsAsync(int id);
    Task<UpdateStatusResult> UpdateStatusAsync(int id, string newStatus);
}
