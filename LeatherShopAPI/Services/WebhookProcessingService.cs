using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.DTOs.WhatsApp;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LeatherShopAPI.Services;

public class WebhookProcessingService : IWebhookProcessingService
{
    private const int MessagePreviewMaxLength = 80;

    /// <summary>
    /// Cache TTL for processed message IDs. Meta retries within ~5 minutes,
    /// so 10 minutes provides adequate margin while keeping memory usage bounded.
    /// </summary>
    private static readonly TimeSpan MessageDeduplicationTtl = TimeSpan.FromMinutes(10);

    private readonly IChatBotService _chatBot;
    private readonly IChatService _chatService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly AppDbContext _db;
    private readonly ILogger<WebhookProcessingService> _logger;
    private readonly IMemoryCache _cache;

    public WebhookProcessingService(
        IChatBotService chatBot,
        IChatService chatService,
        IHubContext<NotificationHub> hubContext,
        AppDbContext db,
        ILogger<WebhookProcessingService> logger,
        IMemoryCache cache)
    {
        _chatBot = chatBot;
        _chatService = chatService;
        _hubContext = hubContext;
        _db = db;
        _logger = logger;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task ProcessWebhookPayloadAsync(WhatsAppWebhookPayload payload, CancellationToken ct = default)
    {
        if (payload.Entry == null) return;

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
                        await ProcessSingleMessageAsync(message, contacts, ct);
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

    private async Task ProcessSingleMessageAsync(
        Message message,
        List<Contact>? contacts,
        CancellationToken ct)
    {
        // --- Idempotency: Deduplicate webhook retries from Meta ---
        // Meta retries webhook delivery if it doesn't receive 200 OK within ~30 seconds.
        // Each retry carries the same message.Id, so we cache processed IDs to skip duplicates.
        var cacheKey = $"wa_msg_{message.Id}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogDebug("Skipping duplicate webhook message {MessageId} from {From}", message.Id, message.From);
            return;
        }

        // Mark as processed before any work — even if processing fails, retries won't help
        _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = MessageDeduplicationTtl
        });

        var from = message.From;
        var contactName = contacts?.FirstOrDefault()?.Profile?.Name ?? "";

        // Extract message content based on type
        var (textBody, interactiveId, interactiveTitle) = ExtractMessageContent(message);

        var phone = PhoneNumberHelper.Normalize(from);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone, ct);
        var incomingContent = textBody ?? interactiveTitle ?? interactiveId ?? "[media]";

        if (customer != null)
        {
            await SaveAndNotifyExistingCustomerAsync(customer, incomingContent, contactName, phone, message.Type, ct);

            // If bot is paused for this customer, skip automated response
            if (await _chatService.IsBotPausedAsync(customer.Id, ct))
            {
                _logger.LogInformation("Bot paused for customer {CustomerId}, skipping bot response", customer.Id);
                return;
            }
        }
        else
        {
            // New customer: save their incoming message BEFORE bot processes it.
            // This ensures correct chronological ordering (incoming message timestamp < bot response timestamps).
            customer = await HandleNewCustomerFirstMessageAsync(phone, incomingContent, contactName, message.Type, ct);
        }

        // Delegate to chatbot for automated response — pass tracked customer to avoid duplicate lookup
        await _chatBot.ProcessMessage(customer, message.Type, textBody, interactiveId, interactiveTitle, ct);
    }

    private static (string? textBody, string? interactiveId, string? interactiveTitle) ExtractMessageContent(
        Message message)
    {
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

        return (textBody, interactiveId, interactiveTitle);
    }

    private async Task SaveAndNotifyExistingCustomerAsync(
        Customer customer, string incomingContent, string contactName, string phone, string messageType, CancellationToken ct)
    {
        var senderName = string.IsNullOrEmpty(contactName) ? phone : contactName;

        var savedMsg = await _chatService.SaveMessageAsync(
            customer.Id, MessageDirection.Incoming, incomingContent, senderName, false, messageType, ct);

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
            content = TruncatePreview(incomingContent),
            timestamp = DateTime.UtcNow
        });
    }

    private async Task<Customer> HandleNewCustomerFirstMessageAsync(
        string phone, string incomingContent, string contactName, string messageType, CancellationToken ct)
    {
        // Create the customer record so we can save their incoming message
        // before the bot processes it (ensures correct chronological ordering).
        var newCustomer = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone, ct);
        if (newCustomer == null)
        {
            newCustomer = new Customer { PhoneNumber = phone, Name = contactName };
            _db.Customers.Add(newCustomer);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Race condition: another webhook already created this customer — reload it.
                _db.Entry(newCustomer).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                newCustomer = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone, ct);
                if (newCustomer == null) throw; // Not a race condition — rethrow the original error
            }
        }

        var senderName = string.IsNullOrEmpty(contactName) ? phone : contactName;

        var savedFirstMsg = await _chatService.SaveMessageAsync(
            newCustomer.Id, MessageDirection.Incoming, incomingContent, senderName, false, messageType, ct);

        // Push the first message to any admin who may have opened this chat
        await _hubContext.Clients.Group($"chat_{newCustomer.Id}").SendAsync("ReceiveMessage", new ChatMessageDto
        {
            Id = savedFirstMsg.Id,
            CustomerId = newCustomer.Id,
            Direction = "Incoming",
            MessageType = savedFirstMsg.MessageType,
            Content = savedFirstMsg.Content,
            SenderName = savedFirstMsg.SenderName,
            IsFromBot = false,
            Timestamp = savedFirstMsg.Timestamp
        });

        // Notify all admins about new conversation
        await _hubContext.Clients.Group("admins").SendAsync("NewChatMessage", new
        {
            customerId = newCustomer.Id,
            customerName = string.IsNullOrEmpty(newCustomer.Name) ? phone : newCustomer.Name,
            content = TruncatePreview(incomingContent),
            timestamp = DateTime.UtcNow
        });

        return newCustomer;
    }

    private static string TruncatePreview(string content) =>
        content.Length > MessagePreviewMaxLength
            ? content[..MessagePreviewMaxLength] + "…"
            : content;
}
