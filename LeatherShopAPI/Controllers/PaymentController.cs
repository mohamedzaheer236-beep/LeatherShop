using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LeatherShopAPI.DTOs.Payment;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

/// <summary>Public (customer-facing). No [Authorize] — customers access payment page and Razorpay calls verify.</summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("fixed")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet("pay/{orderNumber}")]
    public async Task<IActionResult> PaymentPage(string orderNumber)
    {
        var data = await _paymentService.GetPaymentPageDataAsync(orderNumber);
        if (data == null) return NotFound("Order not found or already paid.");

        var safeOrderNumber = WebUtility.HtmlEncode(data.OrderNumber);
        var safeCustomerPhone = WebUtility.HtmlEncode(data.CustomerPhone);
        var safeRazorpayKey = WebUtility.HtmlEncode(data.RazorpayKeyId);

        var itemsHtml = string.Join("", data.Items.Select(i =>
            $"<tr><td>{WebUtility.HtmlEncode(i.ProductName)}</td><td>{i.Quantity}</td><td>₹{i.UnitPrice}</td><td>₹{i.Subtotal}</td></tr>"
        ));

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Pay - {safeOrderNumber}</title>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <script src='https://checkout.razorpay.com/v1/checkout.js'></script>
    <style>
        body {{ font-family: Arial, sans-serif; max-width: 500px; margin: 20px auto; padding: 0 15px; background: #f5f5f5; }}
        .card {{ background: white; border-radius: 12px; padding: 20px; margin-bottom: 15px; box-shadow: 0 2px 8px rgba(0,0,0,.1); }}
        h2 {{ color: #333; margin-top: 0; }}
        table {{ width: 100%; border-collapse: collapse; }}
        th, td {{ padding: 8px; text-align: left; border-bottom: 1px solid #eee; }}
        .total {{ font-size: 24px; font-weight: bold; color: #2e7d32; }}
        .btn {{ display: block; width: 100%; padding: 15px; background: #2e7d32; color: white; border: none; border-radius: 8px; font-size: 18px; cursor: pointer; }}
        .btn:hover {{ background: #1b5e20; }}
    </style>
</head>
<body>
    <div class='card'>
        <h2>Leather Shop</h2>
        <p>Order: <strong>{safeOrderNumber}</strong></p>
        <table>{itemsHtml}</table>
        <br>
        <p class='total'>Total: Rs.{data.TotalAmount}</p>
    </div>
    <button class='btn' onclick='pay()'>Pay Rs.{data.TotalAmount}</button>

    <script>
        function pay() {{
            var options = {{
                key: '{safeRazorpayKey}',
                amount: {data.AmountInPaise},
                currency: 'INR',
                name: 'Leather Shop',
                description: 'Order {safeOrderNumber}',
                handler: function(response) {{
                    fetch('/api/payment/verify', {{
                        method: 'POST',
                        headers: {{ 'Content-Type': 'application/json' }},
                        body: JSON.stringify({{
                            paymentId: response.razorpay_payment_id,
                            orderId: '{safeOrderNumber}',
                            razorpayOrderId: response.razorpay_order_id || '',
                            signature: response.razorpay_signature || ''
                        }})
                    }}).then(function(r) {{
                        if (!r.ok) throw new Error('Verification failed');
                        return r.json();
                    }}).then(function(d) {{
                        document.body.innerHTML = '<div class=card><h2>Payment Successful!</h2><p>Thank you! Check WhatsApp for confirmation.</p></div>';
                    }}).catch(function() {{
                        document.body.innerHTML = '<div class=card><h2>Payment Status Unknown</h2><p>We could not confirm your payment. If money was deducted, please contact us — your payment is safe.</p></div>';
                    }});
                }},
                prefill: {{ contact: '{safeCustomerPhone}' }},
                theme: {{ color: '#2e7d32' }}
            }};
            var rzp = new Razorpay(options);
            rzp.open();
        }}
    </script>
</body>
</html>";

        return Content(html, "text/html");
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment([FromBody] PaymentVerifyDto dto)
    {
        var result = await _paymentService.VerifyPaymentAsync(dto);
        if (result == null)
            return BadRequest(ApiResponse.Fail("Invalid order or payment."));
        return Ok(ApiResponse<PaymentVerifyResultDto>.Ok(result, "Payment verified successfully."));
    }
}
