using LeatherShopAPI.Models.WhatsApp;

namespace LeatherShopAPI.Services.ChatBot.Handlers;

/// <summary>
/// Handles the "Contact Us" chatbot flow.
/// Contact details are read from configuration (ContactInfo section) so they
/// can be updated without any code changes.
/// </summary>
public class ContactHandler
{
    private readonly BotMessageSender _bot;
    private readonly IConfiguration _config;

    public ContactHandler(BotMessageSender bot, IConfiguration config)
    {
        _bot = bot;
        _config = config;
    }

    public async Task SendContactInfo(string to, CancellationToken ct = default)
    {
        var phone       = _config["ContactInfo:Phone"]          ?? "+91-84386-29975";
        var waNumber    = _config["ContactInfo:WhatsAppNumber"] ?? "917305189975";
        var hours       = _config["ContactInfo:BusinessHours"]  ?? "Mon – Sat, 10 AM – 7 PM IST";
        var responseTime = _config["ContactInfo:ResponseTime"]  ?? "within 2 hours during business hours";

        var message =
            "📞 *Contact Cuir Galerie*\n\n" +
            $"📱 *Phone / WhatsApp:* {phone}\n" +
            $"🔗 *Chat on WhatsApp:* https://wa.me/{waNumber}\n\n" +
            $"🕐 *Business Hours:* {hours}\n" +
            $"⏱ *Response Time:* We reply {responseTime}\n\n" +
            "💬 *We can help with:*\n" +
            "• Order status & tracking\n" +
            "• Returns & exchanges\n" +
            "• Product questions\n" +
            "• Custom leather orders\n\n" +
            "_For the fastest response, message us on WhatsApp._";

        await _bot.SendButtons(
            to,
            bodyText: message,
            buttons: new List<ButtonOption>
            {
                new() { Id = "browse_categories", Title = "🏷️ Browse Shop" },
                new() { Id = "my_orders",         Title = "📦 My Orders" },
                new() { Id = "main_menu",         Title = "🏠 Main Menu" }
            },
            ct: ct
        );
    }
}
