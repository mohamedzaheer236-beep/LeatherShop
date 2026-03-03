using System.Threading;
using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

/// <summary>Result of an order status update operation.</summary>
public enum UpdateStatusResult
{
    Success,
    NotFound,
    InvalidStatus,
    InvalidTransition,
    ConcurrencyConflict
}

public interface IOrderService
{
    Task<PaginatedResult<OrderDto>> GetAllAsync(string? status, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task<Order?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<UpdateStatusResult> UpdateStatusAsync(int id, string newStatus, CancellationToken ct = default);
}
