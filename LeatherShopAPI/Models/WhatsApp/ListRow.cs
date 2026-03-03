namespace LeatherShopAPI.Models.WhatsApp;

/// <summary>
/// Represents a row within a WhatsApp interactive list section.
/// </summary>
public class ListRow
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
