using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.Models;

/// <summary>
/// Persisted admin notification for order lifecycle events.
/// Survives server restarts and admin logouts — fetched on login to populate the bell.
/// </summary>
public class AdminNotification
{
    public int Id { get; set; }

    /// <summary>FK to the related order.</summary>
    public int OrderId { get; set; }

    [Required]
    [MaxLength(30)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>"Pending", "Confirmed", "Cancelled" — maps to the order lifecycle event.</summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }
}
