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

    public async Task<PaymentPageDto?> GetPaymentPageDataAsync(string orderNumber)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

        if (order == null || order.IsPaid) return null;

        return new PaymentPageDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerPhone = order.Customer.PhoneNumber,
            TotalAmount = order.TotalAmount,
            AmountInPaise = (int)Math.Round(order.TotalAmount * 100, MidpointRounding.AwayFromZero),
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
        // Look up order by OrderNumber (the payment page now sends OrderNumber, not integer ID)
        Order? order;
        if (int.TryParse(dto.OrderId, out var orderId))
        {
            // Legacy path: integer ID
            order = await _db.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == orderId);
        }
        else
        {
            // Current path: OrderNumber string
            order = await _db.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.OrderNumber == dto.OrderId);
        }

        if (order == null) return null;

        // Verify Razorpay payment signature — MANDATORY in production
        var razorpaySecret = _config["Razorpay:KeySecret"];
        if (string.IsNullOrEmpty(razorpaySecret))
        {
            _logger.LogError("Razorpay:KeySecret is not configured — payment verification REJECTED for order {OrderId}. " +
                "Configure Razorpay:KeySecret to enable payment processing.", order.Id);
            return null; // REJECT — never mark as paid without signature verification
        }

        if (string.IsNullOrEmpty(dto.Signature) || string.IsNullOrEmpty(dto.RazorpayOrderId))
        {
            _logger.LogWarning("Payment verification rejected: missing signature or RazorpayOrderId for order {OrderId}", order.Id);
            return null;
        }

        var payload = $"{dto.RazorpayOrderId}|{dto.PaymentId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(razorpaySecret));
        var computedHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .Replace("-", "").ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHash),
                Encoding.UTF8.GetBytes((dto.Signature ?? "").ToLowerInvariant())))
        {
            _logger.LogWarning("Razorpay signature mismatch for order {OrderId}. Possible tampering.", order.Id);
            return null;
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
                    $"💳 Payment ID: {dto.PaymentId}\n\n" +
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
}
