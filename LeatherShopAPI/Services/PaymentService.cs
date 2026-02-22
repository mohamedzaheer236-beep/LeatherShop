using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Payment;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly IConfiguration _config;

    public PaymentService(AppDbContext db, IWhatsAppService whatsApp, IConfiguration config)
    {
        _db = db;
        _whatsApp = whatsApp;
        _config = config;
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
            RazorpayKeyId = _config["Razorpay:KeyId"] ?? "rzp_test_xxxxx",
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

        // TODO: In production, verify signature with Razorpay secret
        // var generatedSignature = ComputeHmacSha256(razorpayOrderId + "|" + dto.PaymentId, razorpaySecret);
        // if (generatedSignature != dto.Signature) return null;

        order.PaymentId = dto.PaymentId;
        order.IsPaid = true;
        order.Status = OrderStatus.Confirmed;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Notify customer via WhatsApp
        await _whatsApp.SendTextMessage(
            order.Customer.PhoneNumber,
            $"✅ *Payment Received!*\n\n" +
            $"Order: *{order.OrderNumber}*\n" +
            $"Amount: *₹{order.TotalAmount}*\n" +
            $"Payment ID: {dto.PaymentId}\n\n" +
            $"Your order is confirmed! We'll ship it soon. 🚚"
        );

        return new PaymentVerifyResultDto
        {
            Message = "Payment verified",
            OrderNumber = order.OrderNumber
        };
    }
}
