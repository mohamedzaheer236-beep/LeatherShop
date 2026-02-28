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
    public string PaymentId { get; set; } = string.Empty; // Razorpay/Stripe payment ID

    public bool IsPaid { get; set; } = false;

    [MaxLength(500)]
    public string ShippingAddress { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

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
