using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.Models;

public class Customer
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty; // WhatsApp phone number with country code

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    public bool IsSubscribed { get; set; } = true; // For broadcast messages

    /// <summary>When true, the chatbot won't auto-respond. Admin is chatting manually.</summary>
    public bool IsBotPaused { get; set; } = false;

    /// <summary>Bot will auto-resume after this UTC time. Null = not paused or manually paused.</summary>
    public DateTime? BotPausedUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
