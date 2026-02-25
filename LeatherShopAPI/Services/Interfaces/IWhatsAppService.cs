namespace LeatherShopAPI.Services.Interfaces;

public interface IWhatsAppService
{
    Task SendTextMessage(string to, string message);
    Task SendImageMessage(string to, string imageUrl, string? caption = null);
    Task SendListMessage(string to, string headerText, string bodyText, string buttonText, List<ListSection> sections);
    Task SendButtonMessage(string to, string bodyText, List<ButtonOption> buttons);
    Task SendTemplateMessage(string to, string templateName, string languageCode = "en", List<string>? parameters = null, string? imageUrl = null);
    Task<List<WhatsAppTemplate>> GetApprovedTemplates();
}
