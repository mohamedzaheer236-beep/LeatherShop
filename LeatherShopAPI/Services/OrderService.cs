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

    public async Task<PaginatedResult<OrderDto>> GetHistoryAsync(
        int page, int pageSize, string? sortField, string? sortOrder,
        string? customerName, string? customerPhone, string? orderNumber,
        string? status, string? dateSearch,
        decimal? amountMin = null, decimal? amountMax = null, string? isPaid = null,
        CancellationToken ct = default)
    {
        var query = _db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .AsQueryable();

        // Column filters
        if (!string.IsNullOrWhiteSpace(customerName))
            query = query.Where(o => o.Customer.Name.ToLower().Contains(customerName.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(customerPhone))
            query = query.Where(o => o.Customer.PhoneNumber.Contains(customerPhone.Trim()));

        if (!string.IsNullOrWhiteSpace(orderNumber))
            query = query.Where(o => o.OrderNumber.ToLower().Contains(orderNumber.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
            query = query.Where(o => o.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(dateSearch) && DateOnly.TryParse(dateSearch, out var date))
        {
            var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            query = query.Where(o => o.CreatedAt >= start && o.CreatedAt < end);
        }

        if (amountMin.HasValue)
            query = query.Where(o => o.TotalAmount >= amountMin.Value);

        if (amountMax.HasValue)
            query = query.Where(o => o.TotalAmount <= amountMax.Value);

        if (!string.IsNullOrWhiteSpace(isPaid))
        {
            if (bool.TryParse(isPaid, out var paidVal))
                query = query.Where(o => o.IsPaid == paidVal);
        }

        var totalCount = await query.CountAsync(ct);

        // Sorting
        var isDesc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortField?.ToLower()) switch
        {
            "customername" => isDesc ? query.OrderByDescending(o => o.Customer.Name) : query.OrderBy(o => o.Customer.Name),
            "customerphone" => isDesc ? query.OrderByDescending(o => o.Customer.PhoneNumber) : query.OrderBy(o => o.Customer.PhoneNumber),
            "ordernumber" => isDesc ? query.OrderByDescending(o => o.OrderNumber) : query.OrderBy(o => o.OrderNumber),
            "totalamount" => isDesc ? query.OrderByDescending(o => o.TotalAmount) : query.OrderBy(o => o.TotalAmount),
            "status" => isDesc ? query.OrderByDescending(o => o.Status) : query.OrderBy(o => o.Status),
            _ => isDesc ? query.OrderByDescending(o => o.CreatedAt) : query.OrderBy(o => o.CreatedAt),
        };

        var orders = await query
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

        // Notify customer via WhatsApp using the approved order_update UTILITY template.
        // Template messages work outside the 24h session window, unlike plain SendTextMessage.
        // Template params: {{1}} = order number, {{2}} = status text (may include tracking info).
        try
        {
            string statusParam;
            if (status == OrderStatus.Shipped && !string.IsNullOrEmpty(order.TrackingNumber))
            {
                // Pack tracking info into the status parameter so it arrives in one message
                statusParam = string.IsNullOrEmpty(order.TrackingLink)
                    ? $"Shipped 📦\n\nTracking (AWB): {order.TrackingNumber}"
                    : $"Shipped 📦\n\nTracking (AWB): {order.TrackingNumber}\nTrack here: {order.TrackingLink}";
            }
            else
            {
                statusParam = status switch
                {
                    OrderStatus.Confirmed => "Confirmed ✅",
                    OrderStatus.Shipped   => "Shipped 🚚",
                    OrderStatus.Delivered => "Delivered 📦",
                    OrderStatus.Cancelled => "Cancelled ❌",
                    _                    => status.ToString()
                };
            }

            await _whatsApp.SendTemplateMessage(
                order.Customer.PhoneNumber,
                templateName: "order_update",
                languageCode: "en",
                parameters: [order.OrderNumber, statusParam],
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Best-effort WhatsApp notification failed for order {OrderId}", id); }

        return UpdateStatusResult.Success;
    }

    public async Task<UpdateTrackingResult> UpdateTrackingAsync(int id, UpdateTrackingDto dto, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order == null) return UpdateTrackingResult.NotFound;
        if (order.Status != OrderStatus.Shipped) return UpdateTrackingResult.NotShipped;

        order.TrackingNumber = dto.TrackingNumber.Trim();
        order.TrackingLink = string.IsNullOrWhiteSpace(dto.TrackingLink) ? null : dto.TrackingLink.Trim();
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Re-notify customer with corrected tracking info
        try
        {
            var statusParam = string.IsNullOrEmpty(order.TrackingLink)
                ? $"Shipped 📦\n\nTracking (AWB): {order.TrackingNumber}"
                : $"Shipped 📦\n\nTracking (AWB): {order.TrackingNumber}\nTrack here: {order.TrackingLink}";

            await _whatsApp.SendTemplateMessage(
                order.Customer.PhoneNumber,
                templateName: "order_update",
                languageCode: "en",
                parameters: [order.OrderNumber, statusParam],
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Best-effort WhatsApp notification failed for order {OrderId}", id); }

        return UpdateTrackingResult.Success;
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
