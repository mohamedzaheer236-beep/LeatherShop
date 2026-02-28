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
