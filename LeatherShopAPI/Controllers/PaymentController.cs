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
        var (result, data) = await _paymentService.GetPaymentPageDataAsync(orderNumber);

        if (result == PaymentPageResult.NotFound)
            return Content(BuildMessagePage("Order Not Found",
                "This order was not found or has already been paid.",
                "🔍", "#666"), "text/html");

        if (result == PaymentPageResult.Expired)
            return Content(BuildMessagePage("Payment Link Expired",
                "This payment link has expired. Your items have been restored to your cart.\n\nSay <strong>checkout</strong> on WhatsApp to get a new payment link.",
                "⏰", "#e65100"), "text/html");

        var safeOrderNumber = WebUtility.HtmlEncode(data!.OrderNumber);
        var safeCustomerPhone = WebUtility.HtmlEncode(data.CustomerPhone);
        var safeRazorpayKey = WebUtility.HtmlEncode(data.RazorpayKeyId);

        var itemsHtml = string.Join("", data.Items.Select(i =>
            $@"<div class='item'>
                <div class='item-info'>
                    <span class='item-name'>{WebUtility.HtmlEncode(i.ProductName)}</span>
                    <span class='item-qty'>Qty: {i.Quantity}</span>
                </div>
                <span class='item-price'>&#x20B9;{i.Subtotal:F2}</span>
            </div>"
        ));

        // Pass expiry as ISO string for JS countdown (or empty if no expiry)
        var expiresIso = data.ExpiresAtUtc?.ToString("o") ?? "";

        var html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>Pay - {safeOrderNumber}</title>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <script src='https://checkout.razorpay.com/v1/checkout.js'></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%); min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 16px; }}
        .container {{ width: 100%; max-width: 420px; }}
        .card {{ background: #fff; border-radius: 16px; overflow: hidden; box-shadow: 0 20px 60px rgba(0,0,0,.3); }}
        .header {{ background: linear-gradient(135deg, #2e7d32, #1b5e20); padding: 24px; color: #fff; text-align: center; }}
        .header h1 {{ font-size: 20px; font-weight: 600; margin-bottom: 4px; }}
        .header .order-num {{ font-size: 13px; opacity: .85; font-family: monospace; letter-spacing: .5px; }}
        .timer {{ background: rgba(255,255,255,.15); border-radius: 8px; padding: 8px 12px; margin-top: 12px; display: inline-flex; align-items: center; gap: 6px; font-size: 13px; }}
        .timer-dot {{ width: 8px; height: 8px; border-radius: 50%; background: #76ff03; animation: pulse 1s infinite; }}
        @keyframes pulse {{ 0%,100% {{ opacity: 1; }} 50% {{ opacity: .4; }} }}
        .body {{ padding: 20px; }}
        .item {{ display: flex; justify-content: space-between; align-items: center; padding: 12px 0; border-bottom: 1px solid #f0f0f0; }}
        .item:last-child {{ border-bottom: none; }}
        .item-info {{ display: flex; flex-direction: column; gap: 2px; }}
        .item-name {{ font-size: 14px; font-weight: 500; color: #333; }}
        .item-qty {{ font-size: 12px; color: #888; }}
        .item-price {{ font-size: 14px; font-weight: 600; color: #333; white-space: nowrap; }}
        .divider {{ height: 1px; background: #e0e0e0; margin: 16px 0; }}
        .total-row {{ display: flex; justify-content: space-between; align-items: center; }}
        .total-label {{ font-size: 16px; color: #666; }}
        .total-amount {{ font-size: 28px; font-weight: 700; color: #2e7d32; }}
        .btn {{ display: block; width: 100%; padding: 16px; background: linear-gradient(135deg, #2e7d32, #1b5e20); color: #fff; border: none; border-radius: 0 0 16px 16px; font-size: 17px; font-weight: 600; cursor: pointer; transition: opacity .2s; letter-spacing: .3px; }}
        .btn:hover {{ opacity: .92; }}
        .btn:active {{ opacity: .85; }}
        .btn:disabled {{ background: #ccc; cursor: not-allowed; }}
        .secure {{ text-align: center; margin-top: 16px; font-size: 12px; color: rgba(255,255,255,.5); display: flex; align-items: center; justify-content: center; gap: 4px; }}
        .expired-overlay {{ position: fixed; inset: 0; background: rgba(0,0,0,.7); display: flex; align-items: center; justify-content: center; z-index: 100; }}
        .expired-box {{ background: #fff; border-radius: 16px; padding: 32px; text-align: center; max-width: 360px; margin: 16px; }}
        .expired-box h2 {{ color: #e65100; margin: 12px 0 8px; }}
        .expired-box p {{ color: #666; font-size: 14px; line-height: 1.5; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='card'>
            <div class='header'>
                <h1>Leather Shop</h1>
                <div class='order-num'>{safeOrderNumber}</div>
                <div class='timer' id='timer'>
                    <span class='timer-dot'></span>
                    <span id='countdown'>Loading...</span>
                </div>
            </div>
            <div class='body'>
                {itemsHtml}
                <div class='divider'></div>
                <div class='total-row'>
                    <span class='total-label'>Total</span>
                    <span class='total-amount'>&#x20B9;{data.TotalAmount:F2}</span>
                </div>
            </div>
            <button class='btn' id='payBtn' onclick='pay()'>Pay &#x20B9;{data.TotalAmount:F2}</button>
        </div>
        <div class='secure'>🔒 Secured by Razorpay</div>
    </div>

    <div class='expired-overlay' id='expiredOverlay' style='display:none'>
        <div class='expired-box'>
            <div style='font-size:48px'>⏰</div>
            <h2>Link Expired</h2>
            <p>This payment link has expired. Your items have been restored to your cart.<br><br>Say <strong>checkout</strong> on WhatsApp to get a new link.</p>
        </div>
    </div>

    <script>
        var expiresAt = '{expiresIso}';
        var expired = false;

        function startCountdown() {{
            if (!expiresAt) return;
            var endTime = new Date(expiresAt).getTime();

            function update() {{
                var now = Date.now();
                var diff = endTime - now;
                if (diff <= 0) {{
                    expired = true;
                    document.getElementById('countdown').textContent = 'Expired';
                    document.getElementById('payBtn').disabled = true;
                    document.getElementById('expiredOverlay').style.display = 'flex';
                    document.querySelector('.timer-dot').style.background = '#ff1744';
                    document.querySelector('.timer-dot').style.animation = 'none';
                    return;
                }}
                var min = Math.floor(diff / 60000);
                var sec = Math.floor((diff % 60000) / 1000);
                document.getElementById('countdown').textContent = min + ':' + (sec < 10 ? '0' : '') + sec + ' remaining';
                requestAnimationFrame(update);
            }}
            update();
        }}
        startCountdown();

        function pay() {{
            if (expired) {{
                document.getElementById('expiredOverlay').style.display = 'flex';
                return;
            }}
            if (!'{safeRazorpayKey}') {{
                alert('Payment gateway is not configured. Please contact the shop owner.');
                return;
            }}
            var options = {{
                key: '{safeRazorpayKey}',
                amount: {data.AmountInPaise},
                currency: 'INR',
                name: 'Leather Shop',
                description: 'Order {safeOrderNumber}',
                handler: function(response) {{
                    document.getElementById('payBtn').disabled = true;
                    document.getElementById('payBtn').textContent = 'Verifying...';
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
                        document.body.innerHTML = '<div style=""min-height:100vh;display:flex;align-items:center;justify-content:center;padding:16px;background:linear-gradient(135deg,#1a1a2e,#16213e)""><div style=""background:#fff;border-radius:16px;padding:40px;text-align:center;max-width:400px""><div style=""font-size:64px"">✅</div><h2 style=""color:#2e7d32;margin:16px 0 8px"">Payment Successful!</h2><p style=""color:#666;font-size:14px;line-height:1.6"">Thank you for your order.<br>Check WhatsApp for your confirmation.</p></div></div>';
                    }}).catch(function() {{
                        document.body.innerHTML = '<div style=""min-height:100vh;display:flex;align-items:center;justify-content:center;padding:16px;background:linear-gradient(135deg,#1a1a2e,#16213e)""><div style=""background:#fff;border-radius:16px;padding:40px;text-align:center;max-width:400px""><div style=""font-size:64px"">⚠️</div><h2 style=""color:#e65100;margin:16px 0 8px"">Payment Status Unknown</h2><p style=""color:#666;font-size:14px;line-height:1.6"">We could not confirm your payment. If money was deducted, please contact us — your payment is safe.</p></div></div>';
                    }});
                }},
                prefill: {{ contact: '{safeCustomerPhone}' }},
                theme: {{ color: '#2e7d32' }},
                modal: {{ ondismiss: function() {{ /* User closed Razorpay modal — do nothing, button stays active */ }} }}
            }};
            var rzp = new Razorpay(options);
            rzp.on('payment.failed', function(resp) {{
                alert('Payment failed: ' + (resp.error.description || 'Unknown error. Please try again.'));
            }});
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

    /// <summary>Generates a full-page HTML message (used for expired/not-found states).</summary>
    private static string BuildMessagePage(string title, string message, string emoji, string color)
    {
        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>{WebUtility.HtmlEncode(title)}</title>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%); min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 16px; }}
        .card {{ background: #fff; border-radius: 16px; padding: 40px 32px; text-align: center; max-width: 400px; box-shadow: 0 20px 60px rgba(0,0,0,.3); }}
        .emoji {{ font-size: 56px; margin-bottom: 16px; }}
        h2 {{ color: {color}; margin-bottom: 12px; font-size: 22px; }}
        p {{ color: #666; font-size: 14px; line-height: 1.6; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='emoji'>{emoji}</div>
        <h2>{WebUtility.HtmlEncode(title)}</h2>
        <p>{message}</p>
    </div>
</body>
</html>";
    }
}
