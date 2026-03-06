using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using LeatherShopAPI.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace LeatherShopAPI.Services.ChatBot;

/// <summary>
/// Scoped service that wraps WhatsApp message sending + chat history saving + SignalR push.
/// All chatbot handlers share this instance so that _currentCustomerId is set once per request.
/// </summary>
public class BotMessageSender
{
    private readonly IWhatsAppService _whatsApp;
    private readonly IChatService _chatService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<BotMessageSender> _logger;

    /// <summary>
    /// Set per-request by ChatBotService before routing to handlers.
    /// Used to save outgoing bot messages to chat history.
    /// </summary>
    public int? CurrentCustomerId { get; set; }

    public BotMessageSender(
        IWhatsAppService whatsApp,
        IChatService chatService,
        IHubContext<NotificationHub> hubContext,
        ILogger<BotMessageSender> logger)
    {
        _whatsApp = whatsApp;
        _chatService = chatService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendText(string to, string message, CancellationToken ct = default)
    {
        await _whatsApp.SendTextMessage(to, message, ct);
        await SaveAndPushBotMessage(message, "text", ct);
    }

    public async Task SendList(string to, string headerText, string bodyText, string buttonText, List<ListSection> sections, CancellationToken ct = default)
    {
        await _whatsApp.SendListMessage(to, headerText, bodyText, buttonText, sections, ct);
        await SaveAndPushBotMessage($"{headerText}\n{bodyText}", "interactive", ct);
    }

    public async Task SendButtons(string to, string bodyText, List<ButtonOption> buttons, CancellationToken ct = default)
    {
        await _whatsApp.SendButtonMessage(to, bodyText, buttons, ct);
        await SaveAndPushBotMessage(bodyText, "interactive", ct);
    }

    public async Task SendImage(string to, string imageUrl, string? caption, CancellationToken ct = default)
    {
        await _whatsApp.SendImageMessage(to, imageUrl, caption, ct);
        await SaveAndPushBotMessage(caption ?? "[image]", "image", ct);
    }

    public async Task SendVideo(string to, string videoUrl, string? caption, CancellationToken ct = default)
    {
        await _whatsApp.SendVideoMessage(to, videoUrl, caption, ct);
        await SaveAndPushBotMessage(caption ?? "[video]", "video", ct);
    }

    public async Task SendCarousel(string to, string templateName, string bodyText, List<CarouselCard> cards, CancellationToken ct = default)
    {
        await _whatsApp.SendCarouselTemplateMessage(to, templateName, cards, ct: ct);
        await SaveAndPushBotMessage($"[carousel] {bodyText}", "template", ct);
    }

    private async Task SaveAndPushBotMessage(string content, string messageType, CancellationToken ct = default)
    {
        if (!CurrentCustomerId.HasValue) return;
        try
        {
            var saved = await _chatService.SaveMessageAsync(
                CurrentCustomerId.Value, MessageDirection.Outgoing, content, "Bot", true, messageType, ct);

            await _hubContext.Clients.Group($"chat_{CurrentCustomerId.Value}").SendAsync("ReceiveMessage", new ChatMessageDto
            {
                Id = saved.Id,
                CustomerId = CurrentCustomerId.Value,
                Direction = "Outgoing",
                MessageType = saved.MessageType,
                Content = saved.Content,
                SenderName = "Bot",
                IsFromBot = true,
                Timestamp = saved.Timestamp
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save/push bot message for customer {CustomerId}", CurrentCustomerId);
        }
    }
}
