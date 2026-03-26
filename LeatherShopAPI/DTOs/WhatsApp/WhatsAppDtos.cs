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

    /// <summary>
    /// Delivery status updates from Meta (sent, delivered, read, failed).
    /// Sent for every message your business sends via the API.
    /// </summary>
    public List<StatusUpdate>? Statuses { get; set; }
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
    public ButtonReplyContent? Button { get; set; }
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

/// <summary>
/// Represents a quick_reply button response from a template message.
/// Unlike interactive button replies, template quick_reply comes as type "button".
/// </summary>
public class ButtonReplyContent
{
    public string Payload { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Delivery status update from Meta's webhook.
/// Sent for every outgoing message: sent → delivered → read (or failed).
/// The "id" field is the wamid of the original outgoing message.
/// </summary>
public class StatusUpdate
{
    /// <summary>The wamid of the message this status refers to.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Status value: "sent", "delivered", "read", or "failed".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Unix timestamp string of when this status occurred.</summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>Recipient phone number (WhatsApp ID).</summary>
    [JsonPropertyName("recipient_id")]
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>Error details if status is "failed".</summary>
    public List<StatusError>? Errors { get; set; }
}

/// <summary>Error info from a failed status update.</summary>
public class StatusError
{
    public int Code { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    [JsonPropertyName("error_data")]
    public StatusErrorData? ErrorData { get; set; }
}

/// <summary>Nested error data with additional details.</summary>
public class StatusErrorData
{
    public string? Details { get; set; }
}
