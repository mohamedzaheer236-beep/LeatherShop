using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext db, IWhatsAppService whatsApp, IHubContext<NotificationHub> hubContext,
        ILogger<OrderService> logger)
    {
        _db = db;
        _whatsApp = whatsApp;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<PaginatedResult<OrderDto>> GetAllAsync(string? status, int page = 1, int pageSize = 25)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            query = query.Where(o => o.Status == orderStatus);

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResult<OrderDto>
        {
            Items = orders.Select(o => o.ToDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Order?> GetByIdWithDetailsAsync(int id)
    {
        return await _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<UpdateStatusResult> UpdateStatusAsync(int id, string newStatus)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return UpdateStatusResult.NotFound;

        if (!Enum.TryParse<OrderStatus>(newStatus, true, out var status))
            return UpdateStatusResult.InvalidStatus;

        // Validate status transitions — prevent invalid state changes
        var previousStatus = order.Status;
        var validTransitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
            [OrderStatus.Confirmed] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
            [OrderStatus.Shipped] = new[] { OrderStatus.Delivered, OrderStatus.Cancelled },
            [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
            [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
        };

        if (!validTransitions.TryGetValue(previousStatus, out var allowed) || !allowed.Contains(status))
        {
            _logger.LogWarning("Invalid order status transition: {From} -> {To} for order {OrderId}", previousStatus, status, id);
            return UpdateStatusResult.InvalidTransition;
        }

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        // Restore stock when cancelling (only if not already cancelled)
        if (status == OrderStatus.Cancelled && previousStatus != OrderStatus.Cancelled)
        {
            foreach (var item in order.OrderItems)
            {
                item.Product.StockQuantity += item.Quantity;
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while updating order {OrderId} status to {Status}. " +
                "Another operation modified the same product stock.", id, status);
            return UpdateStatusResult.ConcurrencyConflict;
        }

        // Notify customer via WhatsApp (best-effort — don't fail the update)
        try
        {
            var statusEmoji = status switch
            {
                OrderStatus.Confirmed => "✅",
                OrderStatus.Shipped => "🚚",
                OrderStatus.Delivered => "📦",
                OrderStatus.Cancelled => "❌",
                _ => "ℹ️"
            };

            await _whatsApp.SendTextMessage(
                order.Customer.PhoneNumber,
                $"{statusEmoji} *Order Update*\n\nYour order *{order.OrderNumber}* is now: *{status}*\n\nThank you for shopping with us! 🙏"
            );
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Best-effort WhatsApp notification failed for order {OrderId}", id); }

        return UpdateStatusResult.Success;
    }
}
