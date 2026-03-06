using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.DTOs.Payment;
using LeatherShopAPI.Helpers;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public PaymentService(AppDbContext db, IWhatsAppService whatsApp, IHubContext<NotificationHub> hubContext,
        IConfiguration config, ILogger<PaymentService> logger, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _whatsApp = whatsApp;
        _hubContext = hubContext;
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<(PaymentPageResult Result, PaymentPageDto? Data)> GetPaymentPageDataAsync(string orderNumber, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);

        if (order == null || order.IsPaid) return (PaymentPageResult.NotFound, null);

        // Check if payment link has expired
        if (order.PaymentExpiresAt.HasValue && DateTime.UtcNow > order.PaymentExpiresAt.Value)
        {
            await ExpireOrderAndRestoreCartAsync(order, ct);
            return (PaymentPageResult.Expired, null);
        }

        var merchantId = _config["Paytm:MerchantId"];
        var merchantKey = _config["Paytm:MerchantKey"];
        if (string.IsNullOrWhiteSpace(merchantId) || string.IsNullOrWhiteSpace(merchantKey))
            throw new InvalidOperationException(
                "Paytm:MerchantId and Paytm:MerchantKey must be configured. " +
                "Set them in appsettings or environment variables (Paytm__MerchantId, Paytm__MerchantKey).");

        // Call Paytm Initiate Transaction API to get a txnToken
        var txnToken = await InitiatePaytmTransactionAsync(
            merchantId, merchantKey, order.OrderNumber, order.TotalAmount, order.Customer.PhoneNumber, ct);

        return (PaymentPageResult.Ok, new PaymentPageDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerPhone = order.Customer.PhoneNumber,
            TotalAmount = order.TotalAmount,
            AmountInPaise = (int)Math.Round(order.TotalAmount * 100, MidpointRounding.AwayFromZero),
            PaytmMerchantId = merchantId,
            PaytmTxnToken = txnToken,
            ExpiresAtUtc = order.PaymentExpiresAt,
            Items = order.OrderItems.Select(oi => new PaymentPageItemDto
            {
                ProductName = oi.Product.Name,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                Subtotal = oi.UnitPrice * oi.Quantity
            }).ToList()
        });
    }

    /// <summary>
    /// Calls Paytm's Initiate Transaction API to get a transaction token (txnToken).
    /// This token is required by Paytm's checkout.js on the client side.
    /// </summary>
    private async Task<string> InitiatePaytmTransactionAsync(
        string merchantId, string merchantKey, string orderId, decimal amount, string customerPhone, CancellationToken ct = default)
    {
        var paytmEnv = _config["Paytm:Environment"] ?? "production";
        var baseUrl = paytmEnv.Equals("staging", StringComparison.OrdinalIgnoreCase)
            ? "https://securegw-stage.paytm.in"
            : "https://securegw.paytm.in";

        var body = new
        {
            requestType = "Payment",
            mid = merchantId,
            websiteName = paytmEnv.Equals("staging", StringComparison.OrdinalIgnoreCase) ? "WEBSTAGING" : "DEFAULT",
            orderId = orderId,
            txnAmount = new { value = amount.ToString("F2"), currency = "INR" },
            userInfo = new { custId = customerPhone },
            callbackUrl = $"{_config["App:BaseUrl"]}/api/payment/verify"
        };

        var bodyJson = JsonSerializer.Serialize(body);
        var checksum = PaytmChecksum.GenerateSignature(bodyJson, merchantKey);

        var requestPayload = new
        {
            body = body,
            head = new { signature = checksum }
        };

        var url = $"{baseUrl}/theia/api/v1/initiateTransaction?mid={merchantId}&orderId={orderId}";

        var httpClient = _httpClientFactory.CreateClient("Paytm");
        var content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);

        _logger.LogDebug("Paytm Initiate Transaction response for {OrderId}: {Response}", orderId, responseJson);

        var result = JsonSerializer.Deserialize<PaytmInitiateResponse>(responseJson);

        if (result?.Body?.ResultInfo?.ResultCode != "S")
        {
            var errorMsg = result?.Body?.ResultInfo?.ResultMsg ?? "Unknown error";
            _logger.LogError("Paytm Initiate Transaction failed for {OrderId}: {Error}", orderId, errorMsg);
            throw new InvalidOperationException($"Paytm transaction initiation failed: {errorMsg}");
        }

        return result.Body.TxnToken ?? throw new InvalidOperationException("Paytm returned success but no txnToken.");
    }

    /// <summary>
    /// Cancels an expired order, restores product stock, and re-creates cart items
    /// so the customer can checkout again without re-adding products.
    /// </summary>
    internal async Task ExpireOrderAndRestoreCartAsync(Order order, CancellationToken ct = default)
    {
        await OrderExpiryHelper.CancelAndRestoreCartAsync(_db, order, _logger);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PaymentVerifyResultDto?> VerifyPaymentAsync(PaymentVerifyDto dto, CancellationToken ct = default)
    {
        // Look up order by OrderNumber (the payment page sends OrderNumber as OrderId)
        Order? order;
        if (int.TryParse(dto.OrderId, out var orderId))
        {
            order = await _db.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        }
        else
        {
            order = await _db.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.OrderNumber == dto.OrderId, ct);
        }

        if (order == null) return null;

        // --- Idempotency: Skip if order is already paid ---
        // Paytm may call the callback multiple times (user refresh, retries).
        // Return success (idempotent) rather than processing again.
        if (order.IsPaid)
        {
            _logger.LogInformation("Payment verification skipped - order {OrderId} ({OrderNumber}) is already paid",
                order.Id, order.OrderNumber);
            return new PaymentVerifyResultDto
            {
                Message = "Payment already verified",
                OrderNumber = order.OrderNumber
            };
        }

        // Verify payment via Paytm Transaction Status API (server-to-server)
        var merchantId = _config["Paytm:MerchantId"];
        var merchantKey = _config["Paytm:MerchantKey"];
        if (string.IsNullOrEmpty(merchantId) || string.IsNullOrEmpty(merchantKey))
        {
            _logger.LogError("Paytm:MerchantId or Paytm:MerchantKey is not configured - payment verification REJECTED for order {OrderId}. " +
                "Configure Paytm credentials to enable payment processing.", order.Id);
            return null; // REJECT - never mark as paid without server-side verification
        }

        var txnStatus = await GetPaytmTransactionStatusAsync(merchantId, merchantKey, order.OrderNumber, ct);
        if (txnStatus == null)
        {
            _logger.LogWarning("Could not verify Paytm transaction status for order {OrderId}", order.Id);
            return null;
        }

        if (txnStatus.ResultCode != "01" || !string.Equals(txnStatus.Status, "TXN_SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Paytm payment not successful for order {OrderId}. Status: {Status}, Code: {Code}, Msg: {Msg}",
                order.Id, txnStatus.Status, txnStatus.ResultCode, txnStatus.ResultMsg);
            return null;
        }

        // Verify the paid amount matches the order total - protect against amount tampering
        if (decimal.TryParse(txnStatus.TxnAmount, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var paidAmount))
        {
            if (paidAmount != order.TotalAmount)
            {
                _logger.LogError(
                    "PAYMENT AMOUNT MISMATCH for order {OrderId} ({OrderNumber}). Expected: {Expected}, Paid: {Paid}. Rejecting payment.",
                    order.Id, order.OrderNumber, order.TotalAmount, paidAmount);
                return null;
            }
        }
        else
        {
            _logger.LogError(
                "Could not parse TxnAmount '{TxnAmount}' for order {OrderId}. Rejecting payment - amount validation is mandatory.",
                txnStatus.TxnAmount, order.Id);
            return null;
        }

        // Paytm payment is verified via server-to-server API - proceed to confirm.
        // If the order was auto-cancelled due to expiry while the customer
        // was completing payment in the Paytm form, we must honor the payment (money is already charged).
        // Re-confirm the order, re-deduct stock, and clear restored cart items.
        if (order.Status == OrderStatus.Cancelled)
        {
            _logger.LogWarning("Order {OrderNumber} was auto-cancelled (expired) but received valid Paytm payment {TxnId}. Re-confirming.",
                order.OrderNumber, txnStatus.TxnId);

            // Re-deduct stock (it was restored when the order was cancelled)
            var orderItems = await _db.OrderItems
                .Include(oi => oi.Product)
                .Where(oi => oi.OrderId == order.Id)
                .ToListAsync(ct);

            foreach (var item in orderItems)
            {
                item.Product.StockQuantity -= item.Quantity;
            }

            // Remove cart items that were restored (best-effort: remove matching product+image combos)
            var restoredCartItems = await _db.CartItems
                .Where(ci => ci.CustomerId == order.CustomerId)
                .ToListAsync(ct);

            foreach (var orderItem in orderItems)
            {
                var cartItem = restoredCartItems
                    .FirstOrDefault(ci => ci.ProductId == orderItem.ProductId && ci.SelectedImageId == orderItem.SelectedImageId);

                if (cartItem != null)
                {
                    cartItem.Quantity -= orderItem.Quantity;
                    if (cartItem.Quantity <= 0)
                        _db.CartItems.Remove(cartItem);
                    restoredCartItems.Remove(cartItem);
                }
            }
        }

        order.PaymentId = txnStatus.TxnId ?? dto.TransactionId;
        order.IsPaid = true;
        order.Status = OrderStatus.Confirmed;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Notify customer via WhatsApp (best-effort - don't fail the payment)
        try
        {
            await _whatsApp.SendTextMessage(
                order.Customer.PhoneNumber,
                $"✅ *Payment Received!*\n\n" +
                $"Order: *{order.OrderNumber}*\n" +
                $"Amount: *₹{order.TotalAmount}*\n" +
                $"Transaction ID: {order.PaymentId}\n\n" +
                $"Your order is confirmed! We'll ship it soon. 🚚"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send WhatsApp payment notification for order {OrderId}", order.Id);
        }

        // Notify shop owner via WhatsApp (best-effort)
        try
        {
            var ownerPhone = _config["App:OwnerPhone"];
            if (!string.IsNullOrEmpty(ownerPhone))
            {
                await _whatsApp.SendTextMessage(ownerPhone,
                    $"🔔 *New Paid Order!*\n\n" +
                    $"📋 Order: *{order.OrderNumber}*\n" +
                    $"👤 Customer: *{order.Customer.Name}* ({order.Customer.PhoneNumber})\n" +
                    $"💰 Amount: *₹{order.TotalAmount}*\n" +
                    $"💳 Transaction ID: {order.PaymentId}\n\n" +
                    $"Check the dashboard for details.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send owner WhatsApp notification for order {OrderId}", order.Id);
        }

        // Push real-time notification to admin dashboard via SignalR
        try
        {
            await _hubContext.Clients.Group("admins").SendAsync("NewOrder", new OrderNotificationDto
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerName = string.IsNullOrEmpty(order.Customer.Name) ? order.Customer.PhoneNumber : order.Customer.Name,
                Amount = order.TotalAmount,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push SignalR order notification for order {OrderId}", order.Id);
        }

        return new PaymentVerifyResultDto
        {
            Message = "Payment verified",
            OrderNumber = order.OrderNumber
        };
    }

    /// <summary>
    /// Calls Paytm's Transaction Status API to verify a payment server-to-server.
    /// This is the authoritative check - we never trust client-side data alone.
    /// </summary>
    private async Task<PaytmTxnStatusResult?> GetPaytmTransactionStatusAsync(
        string merchantId, string merchantKey, string orderId, CancellationToken ct = default)
    {
        try
        {
            var paytmEnv = _config["Paytm:Environment"] ?? "production";
            var baseUrl = paytmEnv.Equals("staging", StringComparison.OrdinalIgnoreCase)
                ? "https://securegw-stage.paytm.in"
                : "https://securegw.paytm.in";

            var body = new { mid = merchantId, orderId = orderId };
            var bodyJson = JsonSerializer.Serialize(body);
            var checksum = PaytmChecksum.GenerateSignature(bodyJson, merchantKey);

            var requestPayload = new
            {
                body = body,
                head = new { signature = checksum }
            };

            var url = $"{baseUrl}/v3/order/status";
            var httpClient = _httpClientFactory.CreateClient("Paytm");
            var content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, content, ct);
            var responseJson = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("Paytm Transaction Status response for {OrderId}: {Response}", orderId, responseJson);

            var result = JsonSerializer.Deserialize<PaytmStatusApiResponse>(responseJson);
            if (result?.Body == null) return null;

            // Verify the response checksum from Paytm
            var responseBodyJson = JsonSerializer.Serialize(result.Body);
            var responseChecksum = result.Head?.Signature;
            if (string.IsNullOrEmpty(responseChecksum))
            {
                _logger.LogWarning("Missing checksum in Paytm Transaction Status response for order {OrderId}. Rejecting response.", orderId);
                return null;
            }
            if (!PaytmChecksum.VerifySignature(responseBodyJson, merchantKey, responseChecksum))
            {
                _logger.LogWarning("Paytm Transaction Status response checksum mismatch for order {OrderId}. Possible tampering.", orderId);
                return null;
            }

            return new PaytmTxnStatusResult
            {
                Status = result.Body.ResultInfo?.ResultStatus,
                ResultCode = result.Body.ResultInfo?.ResultCode,
                ResultMsg = result.Body.ResultInfo?.ResultMsg,
                TxnId = result.Body.TxnId,
                TxnAmount = result.Body.TxnAmount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Paytm Transaction Status API for order {OrderId}", orderId);
            return null;
        }
    }

    // ---------- Paytm API Response Models ----------

    private class PaytmInitiateResponse
    {
        [JsonPropertyName("body")]
        public PaytmInitiateBody? Body { get; set; }
    }

    private class PaytmInitiateBody
    {
        [JsonPropertyName("resultInfo")]
        public PaytmResultInfo? ResultInfo { get; set; }

        [JsonPropertyName("txnToken")]
        public string? TxnToken { get; set; }
    }

    private class PaytmResultInfo
    {
        [JsonPropertyName("resultStatus")]
        public string? ResultStatus { get; set; }

        [JsonPropertyName("resultCode")]
        public string? ResultCode { get; set; }

        [JsonPropertyName("resultMsg")]
        public string? ResultMsg { get; set; }
    }

    private class PaytmStatusApiResponse
    {
        [JsonPropertyName("head")]
        public PaytmResponseHead? Head { get; set; }

        [JsonPropertyName("body")]
        public PaytmStatusBody? Body { get; set; }
    }

    private class PaytmResponseHead
    {
        [JsonPropertyName("signature")]
        public string? Signature { get; set; }
    }

    private class PaytmStatusBody
    {
        [JsonPropertyName("resultInfo")]
        public PaytmResultInfo? ResultInfo { get; set; }

        [JsonPropertyName("txnId")]
        public string? TxnId { get; set; }

        [JsonPropertyName("orderId")]
        public string? OrderId { get; set; }

        [JsonPropertyName("txnAmount")]
        public string? TxnAmount { get; set; }
    }

    private class PaytmTxnStatusResult
    {
        public string? Status { get; set; }
        public string? ResultCode { get; set; }
        public string? ResultMsg { get; set; }
        public string? TxnId { get; set; }
        public string? TxnAmount { get; set; }
    }
}
