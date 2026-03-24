using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeatherShopAPI.Models;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}

public class Order
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [MaxLength(100)]
    public string PaymentId { get; set; } = string.Empty; // Paytm transaction ID

    public bool IsPaid { get; set; } = false;

    [MaxLength(500)]
    public string ShippingAddress { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the payment link expires. Null = no expiration (legacy orders).</summary>
    public DateTime? PaymentExpiresAt { get; set; }

    /// <summary>Cached Paytm txnToken to avoid "Repeat Request Inconsistent" on payment page retries.</summary>
    [MaxLength(500)]
    public string? PaytmTxnToken { get; set; }

    /// <summary>
    /// Who cancelled this order. Null for non-cancelled orders and legacy orders.
    /// Values: "Customer" (self-service via WhatsApp), "Admin" (dashboard), "System" (expired payment).
    /// </summary>
    [MaxLength(20)]
    public string? CancelledBy { get; set; }

    /// <summary>Courier tracking number (AWB). Set when status transitions to Shipped.</summary>
    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    /// <summary>Courier tracking URL. Set when status transitions to Shipped.</summary>
    [MaxLength(500)]
    public string? TrackingLink { get; set; }

    // Navigation
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

public class OrderItem
{
    [Key]
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>Which ProductImage the customer selected from the carousel. Null = primary image.</summary>
    public int? SelectedImageId { get; set; }
}
