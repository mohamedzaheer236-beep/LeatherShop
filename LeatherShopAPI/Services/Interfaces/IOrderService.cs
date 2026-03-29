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

/// <summary>Result of a customer-initiated order cancellation.</summary>
public enum CancelOrderResult
{
    Success,
    /// <summary>Order not found, or does not belong to this customer.</summary>
    NotFound,
    /// <summary>Order is paid or not in Pending status — cannot be cancelled by customer.</summary>
    NotCancellable,
    /// <summary>A concurrent DB write conflict occurred — caller should ask the customer to retry.</summary>
    ConcurrencyConflict
}

/// <summary>Result of a tracking info update operation.</summary>
public enum UpdateTrackingResult
{
    Success,
    NotFound,
    /// <summary>Tracking can only be edited when the order is in Shipped status.</summary>
    NotShipped
}

public interface IOrderService
{
    Task<PaginatedResult<OrderDto>> GetAllAsync(string? status, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task<PaginatedResult<OrderDto>> GetHistoryAsync(int page, int pageSize, string? sortField, string? sortOrder,
        string? customerName, string? customerPhone, string? orderNumber, string? status, string? dateFrom, string? dateTo,
        decimal? amountMin = null, decimal? amountMax = null, string? isPaid = null, string? cancelledBy = null, CancellationToken ct = default);
    Task<Order?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<UpdateStatusResult> UpdateStatusAsync(int id, UpdateOrderStatusDto dto, CancellationToken ct = default);
    Task<UpdateTrackingResult> UpdateTrackingAsync(int id, UpdateTrackingDto dto, CancellationToken ct = default);

    /// <summary>
    /// Cancels a Pending, unpaid order on behalf of the customer.
    /// Restores product stock and returns items to the customer's cart.
    /// Only succeeds when the order belongs to <paramref name="customerId"/> and is still Pending + unpaid.
    /// </summary>
    Task<CancelOrderResult> CancelByCustomerAsync(int orderId, int customerId, CancellationToken ct = default);
}
