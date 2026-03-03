namespace LeatherShopAPI.Models.WhatsApp;

/// <summary>
/// Represents a single card in a WhatsApp carousel template message.
/// Each card has a header image, a body text parameter, and a quick-reply button payload.
/// </summary>
public class CarouselCard
{
    public string ImageUrl { get; set; } = string.Empty;
    public string BodyParam { get; set; } = string.Empty;
    public string ButtonPayload { get; set; } = string.Empty;
}
