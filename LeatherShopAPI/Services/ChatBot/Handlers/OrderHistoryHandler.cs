using System.Text;
using LeatherShopAPI.Data;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace LeatherShopAPI.Services.ChatBot.Handlers;

/// <summary>
/// Handles order history display for customers.
/// Shows the last 5 orders and, if the most recent is still Pending + unpaid,
/// offers a cancel button so the customer can self-service without admin help.
/// </summary>
public class OrderHistoryHandler
{
    private readonly AppDbContext _db;
    private readonly BotMessageSender _bot;

    public OrderHistoryHandler(AppDbContext db, BotMessageSender bot)
    {
        _db = db;
        _bot = bot;
    }

    public async Task SendOrderHistory(string to, int customerId, CancellationToken ct = default)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        if (!orders.Any())
        {
            await _bot.SendText(to, "📦 You don't have any orders yet. Start shopping!", ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("📦 *Your Recent Orders:*\n");
        foreach (var order in orders)
        {
            var statusIcon = order.Status switch
            {
                OrderStatus.Pending   => "⏳",
                OrderStatus.Confirmed => "✅",
                OrderStatus.Shipped   => "🚚",
                OrderStatus.Delivered => "📦",
                OrderStatus.Cancelled => "❌",
                _                     => "ℹ️"
            };
            sb.AppendLine($"{statusIcon} *{order.OrderNumber}*");
            sb.AppendLine($"   Amount: ₹{order.TotalAmount}");
            sb.AppendLine($"   Status: {order.Status}");
            sb.AppendLine($"   Paid: {(order.IsPaid ? "✅ Yes" : "❌ No")}");
            sb.AppendLine($"   Date: {order.CreatedAt:dd-MMM-yyyy}");
            sb.AppendLine();
        }

        await _bot.SendText(to, sb.ToString(), ct);

        // If the most recent order is still Pending and unpaid, offer a cancel option.
        // Re-use the already-fetched list — no extra DB query needed.
        var cancellable = orders.FirstOrDefault(o =>
            o.Status == OrderStatus.Pending &&
            !o.IsPaid &&
            o.PaymentExpiresAt != null &&
            o.PaymentExpiresAt > DateTime.UtcNow);

        if (cancellable != null)
        {
            await _bot.SendButtons(to,
                $"⚙️ *Pending Order — Action Required*\n\n" +
                $"*{cancellable.OrderNumber}* (₹{cancellable.TotalAmount}) is awaiting payment.\n\n" +
                $"If you no longer want this order, tap *Cancel Order* below — " +
                $"your items will be returned to your cart so you can re-order later.",
                new List<ButtonOption>
                {
                    new() { Id = $"cancel_ord_{cancellable.Id}", Title = "❌ Cancel Order" },
                    new() { Id = "view_cart",                    Title = "🛒 View Cart" },
                    new() { Id = "main_menu",                    Title = "🏠 Menu" }
                },
                ct: ct);
        }
    }
}
