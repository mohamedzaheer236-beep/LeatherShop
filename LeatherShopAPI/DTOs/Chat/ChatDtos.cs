using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.DTOs.Chat;

/// <summary>One conversation row in the chat list (per customer).</summary>
public class ConversationDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsBotPaused { get; set; }
}

/// <summary>A single chat bubble.</summary>
public class ChatMessageDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Direction { get; set; } = string.Empty; // "Incoming" | "Outgoing"
    public string MessageType { get; set; } = "text";
    public string Content { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public bool IsFromBot { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>Admin sends a manual message.</summary>
public class SendMessageDto
{
    [Required(ErrorMessage = "Message is required.")]
    [MinLength(1, ErrorMessage = "Message cannot be empty.")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>Dashboard notification (pushed via SignalR).</summary>
public class OrderNotificationDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    /// <summary>"Pending", "Confirmed", "Cancelled" — tells the frontend what kind of notification to show.</summary>
    public string Status { get; set; } = "Confirmed";
}

/// <summary>A failed outbox message shown to the admin for manual follow-up.</summary>
public class FailedOutboxMessageDto
{
    public int Id { get; set; }
    public string To { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>SignalR event pushed to admins when an outbox message permanently fails.</summary>
public class OutboxFailedEvent
{
    public int OutboxMessageId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
}

/// <summary>Response for toggle-bot endpoint.</summary>
public class ToggleBotResponseDto
{
    public bool IsBotPaused { get; set; }
}

/// <summary>Response for failed-messages/count endpoint.</summary>
public class FailedMessageCountDto
{
    public int Count { get; set; }
}
