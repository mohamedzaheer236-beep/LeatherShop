using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.Models;

/// <summary>
/// Processing state of an outbox message.
/// Stored as string in DB for readability.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>Queued, waiting for the background processor to pick it up.</summary>
    Pending,

    /// <summary>Successfully delivered to WhatsApp.</summary>
    Sent,

    /// <summary>All retry attempts exhausted - requires manual follow-up via admin chat.</summary>
    Failed
}

/// <summary>
/// Transactional Outbox: a WhatsApp message that must be delivered reliably.
///
/// Written to the DB in the SAME SaveChangesAsync() as the business operation
/// (e.g., order creation) - so either both commit or neither does.
///
/// A background service polls for Pending messages and retries with exponential backoff.
/// This survives app restarts, container redeployments, and crashes.
/// </summary>
public class WhatsAppOutboxMessage
{
    [Key]
    public int Id { get; set; }

    /// <summary>Recipient WhatsApp number (e.g., "917904303876")</summary>
    [Required, MaxLength(20)]
    public string To { get; set; } = string.Empty;

    /// <summary>The full message text to send</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Human-readable context for logging and admin visibility (e.g., "Order confirmation for ORD-20260301-A1B2C3")</summary>
    [Required, MaxLength(200)]
    public string Context { get; set; } = string.Empty;

    /// <summary>Current processing status</summary>
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    /// <summary>Number of send attempts so far</summary>
    public int RetryCount { get; set; }

    /// <summary>Maximum retries before giving up</summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>When the next retry should be attempted (null = immediately eligible)</summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>Last error message from a failed attempt</summary>
    [MaxLength(2000)]
    public string? LastError { get; set; }

    /// <summary>When the message was queued</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the message was successfully sent (null if not yet sent)</summary>
    public DateTime? SentAt { get; set; }
}
