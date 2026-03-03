using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.DTOs.Payment;

public class PaymentVerifyDto
{
    [Required(ErrorMessage = "Payment ID is required.")]
    public string PaymentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Order ID is required.")]
    public string OrderId { get; set; } = string.Empty;

    public string RazorpayOrderId { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;
}

public class PaymentPageDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int AmountInPaise { get; set; }
    public string RazorpayKeyId { get; set; } = string.Empty;
    public List<PaymentPageItemDto> Items { get; set; } = new();
    /// <summary>UTC time when this payment link expires. Null = no expiration (legacy).</summary>
    public DateTime? ExpiresAtUtc { get; set; }
}

public class PaymentPageItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public class PaymentVerifyResultDto
{
    public string Message { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
}
