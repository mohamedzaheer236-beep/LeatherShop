using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.Models;

/// <summary>
/// Delivery status of a single broadcast message to a single recipient.
/// Tracks the full lifecycle: Queued → Sent → Delivered → Read (or Failed at any point).
/// Status updates come from Meta's webhook status callbacks (matched by WamId).
/// </summary>
public enum BroadcastDeliveryStatus
{
    /// <summary>Recipient is queued for sending but API call has not been made yet.</summary>
    Queued,

    /// <summary>Meta API accepted the message (HTTP 200). Does NOT mean the user received it.</summary>
    Sent,

    /// <summary>Meta confirmed the message was delivered to the user's device.</summary>
    Delivered,

    /// <summary>Meta confirmed the user opened/read the message.</summary>
    Read,

    /// <summary>Meta reported a permanent delivery failure (e.g., invalid number, user blocked marketing).</summary>
    Failed
}

/// <summary>
/// Per-recipient tracking record for a broadcast.
/// One record per (BroadcastMessage, Phone) pair.
/// Enables identifying exactly who received a broadcast and who didn't.
/// </summary>
public class BroadcastRecipient
{
    [Key]
    public long Id { get; set; }

    /// <summary>FK to the parent broadcast.</summary>
    public int BroadcastMessageId { get; set; }

    /// <summary>Recipient phone number (E.164 without +, e.g., "919876543210").</summary>
    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Meta's unique message ID (wamid) returned when the API accepts the message.
    /// Used to match incoming status webhook callbacks to this recipient.
    /// Null if the send hasn't been attempted yet or if it threw an exception.
    /// </summary>
    [MaxLength(200)]
    public string? WamId { get; set; }

    /// <summary>Current delivery status.</summary>
    public BroadcastDeliveryStatus Status { get; set; } = BroadcastDeliveryStatus.Queued;

    /// <summary>Error details from Meta if the message failed (error code + description).</summary>
    [MaxLength(1000)]
    public string? ErrorDetail { get; set; }

    /// <summary>When this recipient record was created (broadcast enqueued).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the Meta API accepted the message (status changed to Sent).</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>When Meta confirmed delivery to user's device.</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>When Meta confirmed the user read the message.</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>When Meta reported a failure.</summary>
    public DateTime? FailedAt { get; set; }

    /// <summary>
    /// Number of retry attempts already made.
    /// Only retryable errors (131049 - per-user marketing cap) are retried.
    /// Permanent errors (131050 - user opted out) are never retried.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Scheduled time for the next retry attempt. Null means no retry pending.
    /// Set by WebhookProcessingService when error 131049 is detected.
    /// Cleared when retry succeeds or max retries (3) exhausted.
    /// Uses exponential backoff: 24h → 48h → 72h.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    // Navigation
    public BroadcastMessage BroadcastMessage { get; set; } = null!;
}
