using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LeatherShopAPI.DTOs.Payment;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

/// <summary>Public (customer-facing). No [Authorize] - customers access payment page and Paytm handles checkout.</summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("fixed")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    // Thread-safe cache for HTML templates (loaded once on first request)
    private static volatile string? _paymentPageTemplate;
    private static volatile string? _messagePageTemplate;
    private static readonly SemaphoreSlim _templateLock = new(1, 1);

    public PaymentController(IPaymentService paymentService, IConfiguration config, IWebHostEnvironment env)
    {
        _paymentService = paymentService;
        _config = config;
        _env = env;
    }

    [HttpGet("pay/{orderNumber}")]
    public async Task<IActionResult> PaymentPage(string orderNumber, CancellationToken ct)
    {
        var (result, data) = await _paymentService.GetPaymentPageDataAsync(orderNumber, ct);

        if (result == PaymentPageResult.NotFound)
            return Content(await BuildMessagePageAsync("Order Not Found",
                "This order was not found or has already been paid.",
                "&#128269;", "#666", ct), "text/html");

        if (result == PaymentPageResult.Expired)
            return Content(await BuildMessagePageAsync("Payment Link Expired",
                "This payment link has expired. Your items have been restored to your cart.\n\nSay <strong>checkout</strong> on WhatsApp to get a new payment link.",
                "&#9200;", "#e65100", ct), "text/html");

        var safeOrderNumber = WebUtility.HtmlEncode(data!.OrderNumber);
        var safeMerchantId = WebUtility.HtmlEncode(data.PaytmMerchantId);
        var safeTxnToken = WebUtility.HtmlEncode(data.PaytmTxnToken);

        var paytmEnv = _config["Paytm:Environment"] ?? "production";
        var paytmHost = paytmEnv.Equals("staging", StringComparison.OrdinalIgnoreCase)
            ? "securegw-stage.paytm.in"
            : "secure.paytmpayments.com";

        var itemsHtml = string.Join("", data.Items.Select(i =>
            $@"<div class='item'>
                <div class='item-info'>
                    <span class='item-name'>{WebUtility.HtmlEncode(i.ProductName)}</span>
                    <span class='item-qty'>Qty: {i.Quantity}</span>
                </div>
                <span class='item-price'>&#x20B9;{i.Subtotal:F2}</span>
            </div>"
        ));

        var expiresIso = data.ExpiresAtUtc?.ToString("o") ?? "";

        var template = await LoadPaymentPageTemplate(ct);
        var html = template
            .Replace("{{ORDER_NUMBER}}", safeOrderNumber)
            .Replace("{{MERCHANT_ID}}", safeMerchantId)
            .Replace("{{TXN_TOKEN}}", safeTxnToken)
            .Replace("{{PAYTM_HOST}}", paytmHost)
            .Replace("{{ORDER_ITEMS}}", itemsHtml)
            .Replace("{{TOTAL_AMOUNT}}", $"{data.TotalAmount:F2}")
            .Replace("{{EXPIRES_ISO}}", expiresIso);

        return Content(html, "text/html");
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment([FromBody] PaymentVerifyDto dto, CancellationToken ct)
    {
        var result = await _paymentService.VerifyPaymentAsync(dto, ct);
        if (result == null)
            return BadRequest(ApiResponse.Fail("Invalid order or payment."));
        return Ok(ApiResponse<PaymentVerifyResultDto>.Ok(result, "Payment verified successfully."));
    }

    /// <summary>Loads the payment page HTML template from wwwroot/templates, with thread-safe in-memory caching.</summary>
    private async Task<string> LoadPaymentPageTemplate(CancellationToken ct)
    {
        if (_paymentPageTemplate != null) return _paymentPageTemplate;
        await _templateLock.WaitAsync(ct);
        try
        {
            if (_paymentPageTemplate != null) return _paymentPageTemplate;
            var path = Path.Combine(_env.WebRootPath, "templates", "payment-page.html");
            _paymentPageTemplate = await System.IO.File.ReadAllTextAsync(path, ct);
            return _paymentPageTemplate;
        }
        finally { _templateLock.Release(); }
    }

    /// <summary>Loads the message page HTML template from wwwroot/templates, with thread-safe in-memory caching.</summary>
    private async Task<string> LoadMessagePageTemplate(CancellationToken ct)
    {
        if (_messagePageTemplate != null) return _messagePageTemplate;
        await _templateLock.WaitAsync(ct);
        try
        {
            if (_messagePageTemplate != null) return _messagePageTemplate;
            var path = Path.Combine(_env.WebRootPath, "templates", "payment-message.html");
            _messagePageTemplate = await System.IO.File.ReadAllTextAsync(path, ct);
            return _messagePageTemplate;
        }
        finally { _templateLock.Release(); }
    }

    /// <summary>Generates a full-page HTML message (used for expired/not-found states) using the template file.</summary>
    private async Task<string> BuildMessagePageAsync(string title, string message, string emoji, string color, CancellationToken ct)
    {
        var template = await LoadMessagePageTemplate(ct);
        return template
            .Replace("{{TITLE}}", WebUtility.HtmlEncode(title))
            .Replace("{{MESSAGE}}", message)
            .Replace("{{EMOJI}}", emoji)
            .Replace("{{COLOR}}", color);
    }
}
