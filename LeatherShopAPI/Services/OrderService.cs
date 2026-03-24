using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Helpers;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class OrderService : IOrderService
{
    /// <summary>Valid order status transitions - defined once, reused per call.</summary>
    private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidStatusTransitions = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = new[] { OrderStatus.Delivered, OrderStatus.Cancelled },
        [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IAdminNotificationService _adminNotifications;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext db, IWhatsAppService whatsApp, IHubContext<NotificationHub> hubContext,
        IAdminNotificationService adminNotifications, ILogger<OrderService> logger)
    {
        _db = db;
        _whatsApp = whatsApp;
        _hubContext = hubContext;
        _adminNotifications = adminNotifications;
        _logger = logger;
    }

    public async Task<PaginatedResult<OrderDto>> GetAllAsync(string? status, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var baseQuery = _db.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            baseQuery = baseQuery.Where(o => o.Status == orderStatus);

        var totalCount = await baseQuery.CountAsync(ct);

        var orders = await baseQuery
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<OrderDto>
        {
            Items = orders.Select(o => o.ToDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Order?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
    {
        return await _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<UpdateStatusResult> UpdateStatusAsync(int id, UpdateOrderStatusDto dto, CancellationToken ct = default)
    {
        var newStatus = dto.Status;
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order == null) return UpdateStatusResult.NotFound;

        if (!Enum.TryParse<OrderStatus>(newStatus, true, out var status))
            return UpdateStatusResult.InvalidStatus;

        // Validate status transitions - prevent invalid state changes
        var previousStatus = order.Status;

        if (!ValidStatusTransitions.TryGetValue(previousStatus, out var allowed) || !allowed.Contains(status))
        {
            _logger.LogWarning("Invalid order status transition: {From} -> {To} for order {OrderId}", previousStatus, status, id);
            return UpdateStatusResult.InvalidTransition;
        }

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        // Store tracking info when marking as Shipped
        if (status == OrderStatus.Shipped)
        {
            order.TrackingNumber = dto.TrackingNumber?.Trim();
            order.TrackingLink = string.IsNullOrWhiteSpace(dto.TrackingLink) ? null : dto.TrackingLink.Trim();
        }

        // Restore stock when cancelling (only if not already cancelled)
        if (status == OrderStatus.Cancelled && previousStatus != OrderStatus.Cancelled)
        {
            order.CancelledBy = "Admin";
            foreach (var item in order.OrderItems)
            {
                if (item.Product == null)
                {
                    _logger.LogError("OrderItem {OrderItemId} has null Product — cannot restore stock for Order {OrderId}",
                        item.Id, order.Id);
                    continue;
                }
                item.Product.StockQuantity += item.Quantity;
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while updating order {OrderId} status to {Status}. " +
                "Another operation modified the same product stock.", id, status);
            return UpdateStatusResult.ConcurrencyConflict;
        }

        // Notify customer via WhatsApp (best-effort - don't fail the update)
        try
        {
            var customerName = string.IsNullOrEmpty(order.Customer.Name)
                ? "Valued Customer"
                : order.Customer.Name;

            string message;
            if (status == OrderStatus.Shipped && !string.IsNullOrEmpty(order.TrackingNumber))
            {
                var trackingLine = !string.IsNullOrEmpty(order.TrackingLink)
                    ? $"Tracking Number (AWB): {order.TrackingNumber}\nTracking Link: {order.TrackingLink}\n\nYou can track your shipment using the link above."
                    : $"Tracking Number (AWB): {order.TrackingNumber}";

                message =
                    $"Hello {customerName},\n\n" +
                    $"Greetings from Cuir Galerie.\n\n" +
                    $"Your order *{order.OrderNumber}* has been shipped. 📦\n\n" +
                    $"{trackingLine}\n\n" +
                    $"It will be delivered soon to your address.\n\n" +
                    $"If you have any questions, feel free to contact us.\n\n" +
                    $"Thank you for shopping with Cuir Galerie. 🙏";
            }
            else
            {
                var statusEmoji = status switch
                {
                    OrderStatus.Confirmed => "✅",
                    OrderStatus.Shipped   => "🚚",
                    OrderStatus.Delivered => "📦",
                    OrderStatus.Cancelled => "❌",
                    _                    => "ℹ️"
                };
                message = $"{statusEmoji} *Order Update*\n\nYour order *{order.OrderNumber}* is now: *{status}*\n\nThank you for shopping with us! 🙏";
            }

            await _whatsApp.SendTextMessage(order.Customer.PhoneNumber, message);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Best-effort WhatsApp notification failed for order {OrderId}", id); }

        return UpdateStatusResult.Success;
    }

    public async Task<CancelOrderResult> CancelByCustomerAsync(int orderId, int customerId, CancellationToken ct = default)
    {
        // Security gate: filter by both orderId AND customerId in a single query.
        // A customer physically cannot reach another customer's order this way.
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);

        if (order == null)
        {
            _logger.LogWarning("Customer {CustomerId} attempted to cancel order {OrderId} — not found or not owner.", customerId, orderId);
            return CancelOrderResult.NotFound;
        }

        // Only Pending + unpaid orders may be self-cancelled
        if (order.Status != OrderStatus.Pending || order.IsPaid)
        {
            _logger.LogInformation("Customer {CustomerId} attempted to cancel order {OrderId} — not cancellable (Status={Status}, IsPaid={IsPaid}).",
                customerId, orderId, order.Status, order.IsPaid);
            return CancelOrderResult.NotCancellable;
        }

        // Cancel order, restore stock, restore cart — all in memory (no SaveChanges yet)
        await OrderExpiryHelper.CancelAndRestoreCartAsync(_db, order, "Customer", _logger);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while customer {CustomerId} cancelled order {OrderId}.", customerId, orderId);
            return CancelOrderResult.ConcurrencyConflict;
        }

        // Notify admin — best-effort, never fails the cancellation
        try
        {
            var customerName = string.IsNullOrEmpty(order.Customer?.Name)
                ? order.Customer?.PhoneNumber ?? "Customer"
                : order.Customer.Name;
            await _adminNotifications.CreateAndPushAsync(
                order.Id, order.OrderNumber, customerName,
                order.TotalAmount, "Cancelled", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Best-effort admin notification failed for customer-cancelled order {OrderId}.", orderId);
        }

        _logger.LogInformation("Order {OrderNumber} cancelled by customer {CustomerId}.", order.OrderNumber, customerId);
        return CancelOrderResult.Success;
    }
}
