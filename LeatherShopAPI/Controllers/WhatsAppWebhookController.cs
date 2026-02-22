using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.WhatsApp;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IChatBotService _chatBot;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(IChatBotService chatBot, IConfiguration config, ILogger<WhatsAppWebhookController> logger)
    {
        _chatBot = chatBot;
        _config = config;
        _logger = logger;
    }

    [HttpGet("webhook")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var verifyToken = _config["WhatsApp:VerifyToken"];

        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("Webhook verified successfully");
            return Ok(challenge);
        }

        _logger.LogWarning("Webhook verification failed. Token mismatch.");
        return Forbid();
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveMessage([FromBody] WhatsAppWebhookPayload payload)
    {
        try
        {
            if (payload.Entry == null) return Ok();

            foreach (var entry in payload.Entry)
            {
                foreach (var change in entry.Changes)
                {
                    var messages = change.Value.Messages;
                    var contacts = change.Value.Contacts;

                    if (messages == null || !messages.Any()) continue;

                    foreach (var message in messages)
                    {
                        var from = message.From;
                        var contactName = contacts?.FirstOrDefault()?.Profile.Name ?? "";

                        string? textBody = null;
                        string? interactiveId = null;
                        string? interactiveTitle = null;

                        switch (message.Type)
                        {
                            case "text":
                                textBody = message.Text?.Body;
                                break;
                            case "interactive":
                                var reply = message.Interactive?.ListReply ?? message.Interactive?.ButtonReply;
                                interactiveId = reply?.Id;
                                interactiveTitle = reply?.Title;
                                break;
                            default:
                                textBody = "menu";
                                break;
                        }

                        await _chatBot.ProcessMessage(from, contactName, message.Type, textBody, interactiveId, interactiveTitle);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
        }

        return Ok();
    }
}
