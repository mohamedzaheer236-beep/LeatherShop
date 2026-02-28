using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.DTOs.WhatsApp;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IChatBotService _chatBot;
    private readonly IChatService _chatService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IChatBotService chatBot,
        IChatService chatService,
        IHubContext<NotificationHub> hubContext,
        AppDbContext db,
        IConfiguration config,
        ILogger<WhatsAppWebhookController> logger)
    {
        _chatBot = chatBot;
        _chatService = chatService;
        _hubContext = hubContext;
        _db = db;
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
                if (entry.Changes == null) continue;

                foreach (var change in entry.Changes)
                {
                    var messages = change.Value.Messages;
                    var contacts = change.Value.Contacts;

                    if (messages == null || !messages.Any()) continue;

                    foreach (var message in messages)
                    {
                        try
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
                                case "button":
                                    // Template quick_reply buttons come as type "button" with payload
                                    interactiveId = message.Button?.Payload;
                                    interactiveTitle = message.Button?.Text;
                                    break;
                                default:
                                    textBody = "menu";
                                    break;
                            }

                            // --- Save incoming message to chat history ---
                            var phone = LeatherShopAPI.Extensions.PhoneNumberHelper.Normalize(from);
                            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone);
                            var incomingContent = textBody ?? interactiveTitle ?? interactiveId ?? "[media]";

                            if (customer != null)
                            {
                                var savedMsg = await _chatService.SaveMessageAsync(
                                    customer.Id, MessageDirection.Incoming, incomingContent,
                                    string.IsNullOrEmpty(contactName) ? phone : contactName, false, message.Type);

                                // Push to any admin viewing this chat via SignalR
                                await _hubContext.Clients.Group($"chat_{customer.Id}").SendAsync("ReceiveMessage", new ChatMessageDto
                                {
                                    Id = savedMsg.Id,
                                    CustomerId = customer.Id,
                                    Direction = "Incoming",
                                    MessageType = savedMsg.MessageType,
                                    Content = savedMsg.Content,
                                    SenderName = savedMsg.SenderName,
                                    IsFromBot = false,
                                    Timestamp = savedMsg.Timestamp
                                });

                                // Notify all admins about new message (for conversation list refresh)
                                await _hubContext.Clients.Group("admins").SendAsync("NewChatMessage", new
                                {
                                    customerId = customer.Id,
                                    customerName = string.IsNullOrEmpty(customer.Name) ? phone : customer.Name,
                                    content = incomingContent.Length > 80 ? incomingContent[..80] + "…" : incomingContent,
                                    timestamp = DateTime.UtcNow
                                });

                                // --- Check bot pause: if paused, skip bot response ---
                                if (await _chatService.IsBotPausedAsync(customer.Id))
                                {
                                    _logger.LogInformation("Bot paused for customer {CustomerId}, skipping bot response", customer.Id);
                                    continue;
                                }
                            }

                            await _chatBot.ProcessMessage(from, contactName, message.Type, textBody, interactiveId, interactiveTitle);

                            // Save first message for brand-new customers (customer created inside ProcessMessage)
                            if (customer == null)
                            {
                                var newCustomer = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone);
                                if (newCustomer != null)
                                {
                                    await _chatService.SaveMessageAsync(
                                        newCustomer.Id, MessageDirection.Incoming, incomingContent,
                                        string.IsNullOrEmpty(contactName) ? phone : contactName, false, message.Type);
                                }
                            }
                        }
                        catch (Exception msgEx)
                        {
                            _logger.LogError(msgEx, "Error processing message from {From}", message.From);
                            // Continue processing remaining messages in the batch
                        }
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
