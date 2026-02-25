using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
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

    public OrderService(AppDbContext db, IWhatsAppService whatsApp, IHubContext<NotificationHub> hubContext)
    {
        _db = db;
        _whatsApp = whatsApp;
        _hubContext = hubContext;
    }

    public async Task<PaginatedResult<OrderDto>> GetAllAsync(string? status, int page = 1, int pageSize = 25)
    {
        var query = _db.Orders
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

    public async Task<bool> UpdateStatusAsync(int id, string newStatus)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return false;

        if (!Enum.TryParse<OrderStatus>(newStatus, true, out var status))
            return false;

        var previousStatus = order.Status;
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

        await _db.SaveChangesAsync();

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
        catch { /* WhatsApp notification is best-effort */ }

        return true;
    }
}
