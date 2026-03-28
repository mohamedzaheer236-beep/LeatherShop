using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.DTOs.Broadcast;

public class BroadcastRequestDto
{
    [Required(ErrorMessage = "Template name is required.")]
    public string TemplateName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Language code is required.")]
    public string LanguageCode { get; set; } = "en";

    public List<string>? Parameters { get; set; }

    /// <summary>
    /// Header image - accepts either a full URL (https://...) or a server-relative path (/uploads/abc.jpg).
    /// Relative paths are resolved to full public URLs at send time by BroadcastBackgroundService.
    /// </summary>
    public string? ImageUrl { get; set; }

    public List<string>? PhoneNumbers { get; set; }

    /// <summary>Optional category filter — when set (and PhoneNumbers is empty), only subscribers of this category receive the broadcast.</summary>
    public string? Category { get; set; }

    /// <summary>True if the selected template is a carousel type.</summary>
    public bool IsCarousel { get; set; }

    /// <summary>Carousel card data - required when IsCarousel is true.</summary>
    public List<CarouselCardDto>? CarouselCards { get; set; }
}

public class CarouselCardDto
{
    /// <summary>Server-relative image path (e.g., /uploads/abc.jpg) - will be resolved to full URL.</summary>
    [Required]
    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Body text parameter for this card.</summary>
    [Required]
    [MaxLength(1024)]
    public string BodyParam { get; set; } = string.Empty;

    /// <summary>Quick-reply button payload for this card.</summary>
    [MaxLength(256)]
    public string ButtonPayload { get; set; } = string.Empty;
}

public class BroadcastHistoryDto
{
    public int Id { get; set; }
    public string MessageTemplate { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public DateTime SentAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCarousel { get; set; }
}

public class BroadcastResultDto
{
    public string Message { get; set; } = string.Empty;
    public int BroadcastId { get; set; }
    public int TotalRecipients { get; set; }
}

public class BroadcastRecipientDto
{
    public long Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorDetail { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? OriginalSentAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public List<RetryAttemptEntryDto>? RetryHistory { get; set; }
}

public class RetryAttemptEntryDto
{
    public int Attempt { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
}

public class BroadcastDeliverySummaryDto
{
    public int TotalRecipients { get; set; }
    public int Queued { get; set; }
    public int Sent { get; set; }
    public int Delivered { get; set; }
    public int Read { get; set; }
    public int Failed { get; set; }
    public int RetryScheduled { get; set; }
    public int RetryableCount { get; set; }
}

public class BroadcastRetryResultDto
{
    public int ScheduledCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
