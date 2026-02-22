using System.Text.Json.Serialization;

namespace LeatherShopAPI.DTOs.WhatsApp;

public class WhatsAppWebhookPayload
{
    public string Object { get; set; } = string.Empty;
    public List<Entry> Entry { get; set; } = new();
}

public class Entry
{
    public string Id { get; set; } = string.Empty;
    public List<Change> Changes { get; set; } = new();
}

public class Change
{
    public Value Value { get; set; } = new();
    public string Field { get; set; } = string.Empty;
}

public class Value
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = string.Empty;
    public Metadata Metadata { get; set; } = new();
    public List<Contact>? Contacts { get; set; }
    public List<Message>? Messages { get; set; }
}

public class Metadata
{
    [JsonPropertyName("display_phone_number")]
    public string DisplayPhoneNumber { get; set; } = string.Empty;
    [JsonPropertyName("phone_number_id")]
    public string PhoneNumberId { get; set; } = string.Empty;
}

public class Contact
{
    public Profile Profile { get; set; } = new();
    [JsonPropertyName("wa_id")]
    public string WaId { get; set; } = string.Empty;
}

public class Profile
{
    public string Name { get; set; } = string.Empty;
}

public class Message
{
    public string From { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public TextContent? Text { get; set; }
    public Interactive? Interactive { get; set; }
}

public class TextContent
{
    public string Body { get; set; } = string.Empty;
}

public class Interactive
{
    public string Type { get; set; } = string.Empty;
    [JsonPropertyName("list_reply")]
    public InteractiveReply? ListReply { get; set; }
    [JsonPropertyName("button_reply")]
    public InteractiveReply? ButtonReply { get; set; }
}

public class InteractiveReply
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
