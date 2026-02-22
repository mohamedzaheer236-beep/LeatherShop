using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.Models;

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
}
