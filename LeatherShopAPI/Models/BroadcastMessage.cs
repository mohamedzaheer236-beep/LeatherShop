using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.Models;

/// <summary>
/// Broadcast processing status. DB-persisted so broadcasts survive app restarts.
/// </summary>
public enum BroadcastStatus
{
    /// <summary>Created but not yet picked up by the background processor.</summary>
    Pending,

    /// <summary>Currently being processed (sending to recipients).</summary>
    Processing,

    /// <summary>All recipients have been processed (sent or failed).</summary>
    Completed
}

public class BroadcastMessage
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(1000)]
    public string MessageTemplate { get; set; } = string.Empty; // WhatsApp template name

    [MaxLength(2000)]
    public string MessageBody { get; set; } = string.Empty;

    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // ─── DB-backed job data: survives Railway restarts ───

    /// <summary>Processing status - Pending/Processing/Completed.</summary>
    public BroadcastStatus Status { get; set; } = BroadcastStatus.Pending;

    /// <summary>WhatsApp template language code (e.g., "en", "hi").</summary>
    [Required, MaxLength(10)]
    public string LanguageCode { get; set; } = "en";

    /// <summary>JSON-serialized List&lt;string&gt; of template parameters. Null if no parameters.</summary>
    public string? ParametersJson { get; set; }

    /// <summary>Header image URL for image-based templates. Null for text-only.</summary>
    [MaxLength(2000)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// JSON-serialized List&lt;string&gt; of ALL recipient phone numbers.
    /// Stored at broadcast creation time so processing can resume after a restart.
    /// </summary>
    public string RecipientsJson { get; set; } = "[]";

    /// <summary>
    /// JSON-serialized List&lt;string&gt; of phone numbers already processed (sent or failed).
    /// Updated periodically during broadcast. On restart, remaining = Recipients - Processed.
    /// </summary>
    public string ProcessedPhonesJson { get; set; } = "[]";

    /// <summary>True if this broadcast uses a carousel template.</summary>
    public bool IsCarousel { get; set; }

    /// <summary>
    /// JSON-serialized List&lt;CarouselCardDto&gt; for carousel templates.
    /// Each card has ImageUrl, BodyParam, ButtonPayload.
    /// Null for non-carousel templates.
    /// </summary>
    public string? CarouselCardsJson { get; set; }

    // Navigation — per-recipient delivery tracking
    public ICollection<BroadcastRecipient> Recipients { get; set; } = new List<BroadcastRecipient>();
}
