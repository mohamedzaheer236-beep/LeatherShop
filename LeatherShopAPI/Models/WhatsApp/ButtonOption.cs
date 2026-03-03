namespace LeatherShopAPI.Models.WhatsApp;

/// <summary>
/// Represents a quick-reply button in a WhatsApp interactive button message.
/// Maximum 3 buttons per message.
/// </summary>
public class ButtonOption
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
