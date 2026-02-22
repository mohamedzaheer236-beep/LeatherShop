using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.DTOs.Broadcast;

public class BroadcastRequestDto
{
    [Required(ErrorMessage = "Template name is required.")]
    public string TemplateName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Language code is required.")]
    public string LanguageCode { get; set; } = "en";

    public List<string>? Parameters { get; set; }

    [Url(ErrorMessage = "Image URL must be a valid URL.")]
    public string? ImageUrl { get; set; }

    public List<string>? PhoneNumbers { get; set; }
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
