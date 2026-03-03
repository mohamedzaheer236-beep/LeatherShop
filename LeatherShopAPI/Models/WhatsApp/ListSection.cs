namespace LeatherShopAPI.Models.WhatsApp;

/// <summary>
/// Represents a section in a WhatsApp interactive list message.
/// Each section can contain up to 10 rows.
/// </summary>
public class ListSection
{
    public string Title { get; set; } = string.Empty;
    public List<ListRow> Rows { get; set; } = new();
}
