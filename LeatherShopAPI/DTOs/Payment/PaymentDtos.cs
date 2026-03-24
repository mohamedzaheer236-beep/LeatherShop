using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.DTOs.Payment;

public class PaymentVerifyDto
{
    /// <summary>Paytm transaction ID (TXNID) returned after payment.</summary>
    [Required(ErrorMessage = "Transaction ID is required.")]
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>Order number used as Paytm ORDER_ID.</summary>
    [Required(ErrorMessage = "Order ID is required.")]
    public string OrderId { get; set; } = string.Empty;
}

public class PaymentPageDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int AmountInPaise { get; set; }

    /// <summary>Paytm Merchant ID (MID) - injected into client-side JS for checkout initialization.</summary>
    public string PaytmMerchantId { get; set; } = string.Empty;

    /// <summary>Paytm transaction token - obtained from Initiate Transaction API, required by checkout.js.</summary>
    public string PaytmTxnToken { get; set; } = string.Empty;

    /// <summary>The orderId sent to Paytm (may include a retry suffix like _R1711324800). Checkout JS must use this, not OrderNumber.</summary>
    public string PaytmOrderId { get; set; } = string.Empty;

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
