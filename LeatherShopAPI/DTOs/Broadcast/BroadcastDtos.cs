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
    /// Header image — accepts either a full URL (https://...) or a server-relative path (/uploads/abc.jpg).
    /// Relative paths are resolved to full public URLs at send time by BroadcastBackgroundService.
    /// </summary>
    public string? ImageUrl { get; set; }

    public List<string>? PhoneNumbers { get; set; }

    /// <summary>True if the selected template is a carousel type.</summary>
    public bool IsCarousel { get; set; }

    /// <summary>Carousel card data — required when IsCarousel is true.</summary>
    public List<CarouselCardDto>? CarouselCards { get; set; }
}

public class CarouselCardDto
{
    /// <summary>Server-relative image path (e.g., /uploads/abc.jpg) — will be resolved to full URL.</summary>
    [Required]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Body text parameter for this card.</summary>
    [Required]
    public string BodyParam { get; set; } = string.Empty;

    /// <summary>Quick-reply button payload for this card.</summary>
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
    public DateTime SentAt { get; set; }
}

public class BroadcastResultDto
{
    public string Message { get; set; } = string.Empty;
    public int BroadcastId { get; set; }
    public int TotalRecipients { get; set; }
}
