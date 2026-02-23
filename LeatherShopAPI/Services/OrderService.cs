using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;

    public OrderService(AppDbContext db, IWhatsAppService whatsApp)
    {
        _db = db;
        _whatsApp = whatsApp;
    }

    public async Task<List<OrderDto>> GetAllAsync(string? status)
    {
        var query = _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            query = query.Where(o => o.Status == orderStatus);

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        return orders.Select(o => o.ToDto()).ToList();
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

        // Notify customer via WhatsApp
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

        return true;
    }
}
