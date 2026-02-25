using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.DTOs.Payment;
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

    public PaymentService(AppDbContext db, IWhatsAppService whatsApp, IHubContext<NotificationHub> hubContext,
        IConfiguration config, ILogger<PaymentService> logger)
    {
        _db = db;
        _whatsApp = whatsApp;
        _hubContext = hubContext;
        _config = config;
        _logger = logger;
    }

    public async Task<PaymentPageDto?> GetPaymentPageDataAsync(int orderId)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || order.IsPaid) return null;

        return new PaymentPageDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerPhone = order.Customer.PhoneNumber,
            TotalAmount = order.TotalAmount,
            AmountInPaise = (int)(order.TotalAmount * 100),
            RazorpayKeyId = _config["Razorpay:KeyId"] ?? throw new InvalidOperationException("Razorpay:KeyId not configured. Set it in appsettings or environment variables."),
            Items = order.OrderItems.Select(oi => new PaymentPageItemDto
            {
                ProductName = oi.Product.Name,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                Subtotal = oi.UnitPrice * oi.Quantity
            }).ToList()
        };
    }

    public async Task<PaymentVerifyResultDto?> VerifyPaymentAsync(PaymentVerifyDto dto)
    {
        if (!int.TryParse(dto.OrderId, out var orderId))
            return null;

        var order = await _db.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return null;

        // Verify Razorpay payment signature
        var razorpaySecret = _config["Razorpay:KeySecret"] ?? "";
        if (!string.IsNullOrEmpty(razorpaySecret))
        {
            // KeySecret is configured — signature verification is MANDATORY
            if (string.IsNullOrEmpty(dto.Signature) || string.IsNullOrEmpty(dto.RazorpayOrderId))
            {
                _logger.LogWarning("Payment verification rejected: missing signature or RazorpayOrderId for order {OrderId}", orderId);
                return null;
            }

            var payload = $"{dto.RazorpayOrderId}|{dto.PaymentId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(razorpaySecret));
            var computedHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
                .Replace("-", "").ToLowerInvariant();
            if (computedHash != dto.Signature)
            {
                _logger.LogWarning("Razorpay signature mismatch for order {OrderId}. Possible tampering.", orderId);
                return null;
            }
        }
        else
        {
            _logger.LogWarning("Razorpay:KeySecret not configured — signature verification SKIPPED for order {OrderId}. Set Razorpay:KeySecret in appsettings for production.", orderId);
        }

        order.PaymentId = dto.PaymentId;
        order.IsPaid = true;
        order.Status = OrderStatus.Confirmed;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Notify customer via WhatsApp (best-effort — don't fail the payment)
        try
        {
            await _whatsApp.SendTextMessage(
                order.Customer.PhoneNumber,
                $"✅ *Payment Received!*\n\n" +
                $"Order: *{order.OrderNumber}*\n" +
                $"Amount: *₹{order.TotalAmount}*\n" +
                $"Payment ID: {dto.PaymentId}\n\n" +
                $"Your order is confirmed! We'll ship it soon. 🚚"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send WhatsApp payment notification for order {OrderId}", orderId);
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
                    $"💳 Payment ID: {dto.PaymentId}\n\n" +
                    $"Check the dashboard for details.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send owner WhatsApp notification for order {OrderId}", orderId);
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
            _logger.LogWarning(ex, "Failed to push SignalR order notification for order {OrderId}", orderId);
        }

        return new PaymentVerifyResultDto
        {
            Message = "Payment verified",
            OrderNumber = order.OrderNumber
        };
    }
}
