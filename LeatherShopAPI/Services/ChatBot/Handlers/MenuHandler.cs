using LeatherShopAPI.Data;
using LeatherShopAPI.Models.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace LeatherShopAPI.Services.ChatBot.Handlers;

/// <summary>
/// Handles main menu display and category browsing.
/// </summary>
public class MenuHandler
{
    private readonly AppDbContext _db;
    private readonly BotMessageSender _bot;

    public MenuHandler(AppDbContext db, BotMessageSender bot)
    {
        _db = db;
        _bot = bot;
    }

    public async Task SendMainMenu(string to, string customerName, CancellationToken ct = default)
    {
        var greeting = string.IsNullOrEmpty(customerName) ? "Welcome!" : $"Hello {customerName}! 👋";

        await _bot.SendList(
            to,
            headerText: "🛍️ Cuir Galerie",
            bodyText: $"{greeting}\n\nWe offer premium handcrafted leather products.\n\nWhat would you like to do?",
            buttonText: "📋 View Menu",
            sections: new List<ListSection>
            {
                new()
                {
                    Title = "Shop",
                    Rows = new List<ListRow>
                    {
                        new() { Id = "browse_categories", Title = "🏷️ Browse Categories", Description = "Wallets, Belts, Bags, Shoes & more" },
                        new() { Id = "view_cart", Title = "🛒 View Cart", Description = "See items in your cart" },
                        new() { Id = "checkout", Title = "💳 Checkout", Description = "Place your order & pay" }
                    }
                },
                new()
                {
                    Title = "Account",
                    Rows = new List<ListRow>
                    {
                        new() { Id = "my_orders", Title = "📦 My Orders", Description = "Track your order status" }
                    }
                },
                new()
                {
                    Title = "Support",
                    Rows = new List<ListRow>
                    {
                        new() { Id = "contact_us", Title = "📞 Contact Us", Description = "Phone, WhatsApp & business hours" }
                    }
                }
            },
            ct: ct
        );
    }

    public async Task SendCategoryList(string to, CancellationToken ct = default)
    {
        var categories = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity > 0)
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        if (!categories.Any())
        {
            await _bot.SendText(to, "Sorry, no products available right now. Please check back later!", ct);
            return;
        }

        var rows = categories.Select(cat => new ListRow
        {
            Id = $"cat_{cat.ToLower().Replace(" ", "_")}",
            Title = cat,
            Description = $"Browse {cat} collection"
        }).ToList();

        await _bot.SendList(
            to,
            headerText: "📂 Categories",
            bodyText: "Select a category to browse products:",
            buttonText: "🏷️ Categories",
            sections: new List<ListSection>
            {
                new() { Title = "Available Categories", Rows = rows }
            },
            ct: ct
        );
    }
}
