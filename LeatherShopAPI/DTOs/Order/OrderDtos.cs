using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.DTOs.Order;

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public string? ShippingAddress { get; set; }
    public string? PaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Who cancelled this order. "Customer", "Admin", "System", or null for non-cancelled/legacy orders.</summary>
    public string? CancelledBy { get; set; }
    /// <summary>Courier tracking number (AWB). Present when status is Shipped or Delivered.</summary>
    public string? TrackingNumber { get; set; }
    /// <summary>Courier tracking URL. Present when status is Shipped or Delivered.</summary>
    public string? TrackingLink { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    /// <summary>Resolved image URL: selected image from carousel, or primary product image as fallback.</summary>
    public string? SelectedImageUrl { get; set; }
}

public class UpdateOrderStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;

    /// <summary>Required when Status = "Shipped". Courier AWB/tracking number.</summary>
    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    /// <summary>Optional when Status = "Shipped". Direct tracking URL.</summary>
    [MaxLength(500)]
    [Url]
    public string? TrackingLink { get; set; }
}

public class UpdateTrackingDto
{
    /// <summary>Corrected courier AWB/tracking number.</summary>
    [Required]
    [MaxLength(100)]
    [RegularExpression(@"\S+.*", ErrorMessage = "Tracking number cannot be blank.")]
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>Optional corrected tracking URL.</summary>
    [MaxLength(500)]
    [Url]
    public string? TrackingLink { get; set; }
}
