using System.Threading;
using LeatherShopAPI.Models.WhatsApp;

namespace LeatherShopAPI.Services.Interfaces;

public interface IWhatsAppService
{
    Task SendTextMessage(string to, string message, CancellationToken ct = default);
    Task SendImageMessage(string to, string imageUrl, string? caption = null, CancellationToken ct = default);
    Task SendVideoMessage(string to, string videoUrl, string? caption = null, CancellationToken ct = default);
    Task SendListMessage(string to, string headerText, string bodyText, string buttonText, List<ListSection> sections, CancellationToken ct = default);
    Task SendButtonMessage(string to, string bodyText, List<ButtonOption> buttons, CancellationToken ct = default);
    /// <summary>Returns the wamid (Meta message ID) on success.</summary>
    Task<string?> SendTemplateMessage(string to, string templateName, string languageCode = "en", List<string>? parameters = null, string? imageUrl = null, CancellationToken ct = default);
    /// <summary>Returns the wamid (Meta message ID) on success.</summary>
    Task<string?> SendCarouselTemplateMessage(string to, string templateName, List<CarouselCard> cards, string languageCode = "en", CancellationToken ct = default);
    Task<List<WhatsAppTemplate>> GetApprovedTemplates(CancellationToken ct = default);
}
