using System.Text;
using LeatherShopAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace LeatherShopAPI.Services.ChatBot.Handlers;

/// <summary>
/// Handles order history display for customers.
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
            sb.AppendLine($"🔸 *{order.OrderNumber}*");
            sb.AppendLine($"   Amount: ₹{order.TotalAmount}");
            sb.AppendLine($"   Status: {order.Status}");
            sb.AppendLine($"   Paid: {(order.IsPaid ? "✅ Yes" : "❌ No")}");
            sb.AppendLine($"   Date: {order.CreatedAt:dd-MMM-yyyy}");
            sb.AppendLine();
        }

        await _bot.SendText(to, sb.ToString(), ct);
    }
}
