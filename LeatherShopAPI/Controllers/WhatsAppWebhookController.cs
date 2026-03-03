using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LeatherShopAPI.DTOs.WhatsApp;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiController]
[Route("api/whatsapp")]
[EnableRateLimiting("fixed")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IWebhookProcessingService _webhookService;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppWebhookController> _logger;
    private readonly IWebHostEnvironment _env;

    public WhatsAppWebhookController(
        IWebhookProcessingService webhookService,
        IConfiguration config,
        ILogger<WhatsAppWebhookController> logger,
        IWebHostEnvironment env)
    {
        _webhookService = webhookService;
        _config = config;
        _logger = logger;
        _env = env;
    }

    [HttpGet("webhook")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var verifyToken = _config["WhatsApp:VerifyToken"];

        // Guard: reject if verify token is not configured (prevents null == null match)
        if (string.IsNullOrEmpty(verifyToken))
        {
            _logger.LogError("WhatsApp:VerifyToken is not configured — cannot verify webhook");
            return StatusCode(500);
        }

        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("Webhook verified successfully");
            return Ok(challenge);
        }

        _logger.LogWarning("Webhook verification failed. Token mismatch.");
        return Forbid();
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveMessage(CancellationToken ct)
    {
        // --- Webhook Signature Verification ---
        if (!await VerifySignatureAsync(ct))
            return Unauthorized();

        // Deserialize the payload
        WhatsAppWebhookPayload? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<WhatsAppWebhookPayload>(Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Webhook rejected: invalid JSON payload");
            return BadRequest();
        }

        if (payload == null) return Ok();

        try
        {
            await _webhookService.ProcessWebhookPayloadAsync(payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
        }

        return Ok();
    }

    /// <summary>
    /// Verifies the X-Hub-Signature-256 HMAC header from Meta.
    /// Returns true if the signature is valid (or verification is skipped in Development).
    /// Returns false if the signature is invalid or missing in non-Development environments.
    /// </summary>
    private async Task<bool> VerifySignatureAsync(CancellationToken ct)
    {
        var appSecret = _config["WhatsApp:AppSecret"];

        if (!string.IsNullOrEmpty(appSecret))
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(ct);
            Request.Body.Position = 0; // rewind for deserialization

            var signatureHeader = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256="))
            {
                _logger.LogWarning("Webhook rejected: missing or malformed X-Hub-Signature-256 header");
                return false;
            }

            var expectedSignature = signatureHeader["sha256=".Length..].ToLowerInvariant();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            var computedHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)))
                .Replace("-", "").ToLowerInvariant();

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computedHash),
                    Encoding.UTF8.GetBytes(expectedSignature)))
            {
                _logger.LogWarning("Webhook rejected: X-Hub-Signature-256 mismatch — possible forged payload");
                return false;
            }

            return true;
        }

        if (!_env.IsDevelopment())
        {
            _logger.LogError("WhatsApp:AppSecret not configured — rejecting webhook in non-Development environment");
            return false;
        }

        _logger.LogWarning("WhatsApp:AppSecret not configured — webhook signature verification SKIPPED (Development only).");
        return true;
    }
}
