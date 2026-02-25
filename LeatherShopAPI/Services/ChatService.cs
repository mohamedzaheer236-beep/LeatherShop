using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<ChatService> _logger;

    public ChatService(AppDbContext db, IWhatsAppService whatsApp, ILogger<ChatService> logger)
    {
        _db = db;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task<List<ConversationDto>> GetConversationsAsync(string? search)
    {
        var query = _db.Customers
            .Include(c => c.Orders)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s) || c.PhoneNumber.Contains(s));
        }

        // Only include customers that have at least one chat message
        var customers = await query
            .Where(c => _db.ChatMessages.Any(m => m.CustomerId == c.Id))
            .ToListAsync();

        var conversations = new List<ConversationDto>();
        foreach (var c in customers)
        {
            var lastMsg = await _db.ChatMessages
                .Where(m => m.CustomerId == c.Id)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();

            if (lastMsg == null) continue;

            // Count incoming messages that arrived after the last admin outgoing message
            var lastAdminMsg = await _db.ChatMessages
                .Where(m => m.CustomerId == c.Id
                         && m.Direction == MessageDirection.Outgoing
                         && !m.IsFromBot)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();

            var unreadCutoff = lastAdminMsg?.Timestamp ?? DateTime.MinValue;
            var unread = await _db.ChatMessages
                .CountAsync(m => m.CustomerId == c.Id
                              && m.Direction == MessageDirection.Incoming
                              && m.Timestamp > unreadCutoff);

            conversations.Add(new ConversationDto
            {
                CustomerId = c.Id,
                CustomerName = string.IsNullOrEmpty(c.Name) ? c.PhoneNumber : c.Name,
                PhoneNumber = c.PhoneNumber,
                LastMessage = lastMsg.Content.Length > 80 ? lastMsg.Content[..80] + "…" : lastMsg.Content,
                LastMessageAt = lastMsg.Timestamp,
                UnreadCount = unread,
                IsBotPaused = IsBotCurrentlyPaused(c)
            });
        }

        return conversations.OrderByDescending(c => c.LastMessageAt).ToList();
    }

    public async Task<PaginatedResult<ChatMessageDto>> GetMessagesAsync(int customerId, int page = 1, int pageSize = 50)
    {
        var query = _db.ChatMessages
            .Where(m => m.CustomerId == customerId);

        var totalCount = await query.CountAsync();

        var messages = await query
            .OrderByDescending(m => m.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                CustomerId = m.CustomerId,
                Direction = m.Direction.ToString(),
                MessageType = m.MessageType,
                Content = m.Content,
                SenderName = m.SenderName,
                IsFromBot = m.IsFromBot,
                Timestamp = m.Timestamp
            })
            .ToListAsync();

        // Return in chronological order (oldest first) for display
        messages.Reverse();

        return new PaginatedResult<ChatMessageDto>
        {
            Items = messages,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ChatMessageDto> SendMessageAsync(int customerId, string text)
    {
        var customer = await _db.Customers.FindAsync(customerId)
            ?? throw new KeyNotFoundException($"Customer {customerId} not found");

        // Send via WhatsApp
        await _whatsApp.SendTextMessage(customer.PhoneNumber, text);

        // Pause bot for 30 minutes
        customer.IsBotPaused = true;
        customer.BotPausedUntil = DateTime.UtcNow.AddMinutes(30);

        // Save to chat history
        var msg = await SaveMessageAsync(customerId, MessageDirection.Outgoing, text, "Admin", false);

        return new ChatMessageDto
        {
            Id = msg.Id,
            CustomerId = customerId,
            Direction = msg.Direction.ToString(),
            MessageType = msg.MessageType,
            Content = msg.Content,
            SenderName = msg.SenderName,
            IsFromBot = false,
            Timestamp = msg.Timestamp
        };
    }

    public async Task<bool> ToggleBotAsync(int customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null) return false;

        if (IsBotCurrentlyPaused(customer))
        {
            // Resume bot
            customer.IsBotPaused = false;
            customer.BotPausedUntil = null;
        }
        else
        {
            // Pause bot indefinitely (until manually resumed)
            customer.IsBotPaused = true;
            customer.BotPausedUntil = null; // null = indefinite pause
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Bot {State} for customer {CustomerId}",
            customer.IsBotPaused ? "paused" : "resumed", customerId);

        return customer.IsBotPaused;
    }

    public async Task<ChatMessage> SaveMessageAsync(int customerId, MessageDirection direction, string content,
        string senderName, bool isFromBot, string messageType = "text")
    {
        var message = new ChatMessage
        {
            CustomerId = customerId,
            Direction = direction,
            MessageType = messageType,
            Content = content,
            SenderName = senderName,
            IsFromBot = isFromBot,
            Timestamp = DateTime.UtcNow
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();

        return message;
    }

    public async Task<bool> IsBotPausedAsync(int customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null) return false;
        return IsBotCurrentlyPaused(customer);
    }

    public async Task<bool> DeleteConversationAsync(int customerId)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.CustomerId == customerId)
            .ToListAsync();

        if (!messages.Any()) return false;

        _db.ChatMessages.RemoveRange(messages);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted {Count} chat messages for customer {CustomerId}", messages.Count, customerId);
        return true;
    }

    /// <summary>
    /// Checks if the bot is paused, auto-resumes if BotPausedUntil has expired.
    /// </summary>
    private static bool IsBotCurrentlyPaused(Customer customer)
    {
        if (!customer.IsBotPaused) return false;

        // If BotPausedUntil is set and has expired, bot should resume
        if (customer.BotPausedUntil.HasValue && customer.BotPausedUntil.Value <= DateTime.UtcNow)
        {
            customer.IsBotPaused = false;
            customer.BotPausedUntil = null;
            return false;
        }

        return true;
    }
}
