using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class ChatService : IChatService
{
    private const int BotPauseMinutes = 30;

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
        var query = _db.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{search}%") || c.PhoneNumber.Contains(search));
        }

        // Single query: project all conversation data in the DB — eliminates N+1 per-customer queries
        var conversations = await query
            .Where(c => _db.ChatMessages.Any(m => m.CustomerId == c.Id))
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.PhoneNumber,
                c.IsBotPaused,
                c.BotPausedUntil,
                LastMessageContent = _db.ChatMessages
                    .Where(m => m.CustomerId == c.Id)
                    .OrderByDescending(m => m.Timestamp)
                    .Select(m => m.Content)
                    .FirstOrDefault(),
                LastMessageAt = _db.ChatMessages
                    .Where(m => m.CustomerId == c.Id)
                    .Max(m => (DateTime?)m.Timestamp),
                UnreadCount = _db.ChatMessages
                    .Count(m => m.CustomerId == c.Id
                        && m.Direction == MessageDirection.Incoming
                        && m.Timestamp > (
                            _db.ChatMessages
                                .Where(m2 => m2.CustomerId == c.Id
                                    && m2.Direction == MessageDirection.Outgoing
                                    && !m2.IsFromBot)
                                .Max(m2 => (DateTime?)m2.Timestamp)
                            ?? DateTime.MinValue
                        ))
            })
            .OrderByDescending(x => x.LastMessageAt)
            .ToListAsync();

        return conversations.Select(c => new ConversationDto
        {
            CustomerId = c.Id,
            CustomerName = string.IsNullOrEmpty(c.Name) ? c.PhoneNumber : c.Name,
            PhoneNumber = c.PhoneNumber,
            LastMessage = (c.LastMessageContent?.Length ?? 0) > 80
                ? c.LastMessageContent![..80] + "…"
                : c.LastMessageContent ?? "",
            LastMessageAt = c.LastMessageAt ?? DateTime.MinValue,
            UnreadCount = c.UnreadCount,
            IsBotPaused = c.IsBotPaused && (!c.BotPausedUntil.HasValue || c.BotPausedUntil.Value > DateTime.UtcNow)
        }).ToList();
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
        customer.BotPausedUntil = DateTime.UtcNow.AddMinutes(BotPauseMinutes);

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

        if (IsBotEffectivelyPaused(customer))
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
        return await CheckAndAutoResumeBotAsync(customer);
    }

    public async Task<bool> DeleteConversationAsync(int customerId)
    {
        var deletedCount = await _db.ChatMessages
            .Where(m => m.CustomerId == customerId)
            .ExecuteDeleteAsync();

        if (deletedCount == 0) return false;

        _logger.LogInformation("Deleted {Count} chat messages for customer {CustomerId}", deletedCount, customerId);
        return true;
    }

    /// <summary>
    /// Pure read-only check: is the bot effectively paused right now?
    /// Does NOT persist auto-resume to the DB (use CheckAndAutoResumeBotAsync for that).
    /// </summary>
    private static bool IsBotEffectivelyPaused(Customer customer)
    {
        if (!customer.IsBotPaused) return false;
        if (customer.BotPausedUntil.HasValue && customer.BotPausedUntil.Value <= DateTime.UtcNow)
            return false; // expired — effectively not paused
        return true;
    }

    /// <summary>
    /// Checks if the bot is paused. If BotPausedUntil has expired, auto-resumes
    /// and persists the change to the database.
    /// </summary>
    private async Task<bool> CheckAndAutoResumeBotAsync(Customer customer)
    {
        if (!customer.IsBotPaused) return false;

        // If BotPausedUntil is set and has expired, bot should resume
        if (customer.BotPausedUntil.HasValue && customer.BotPausedUntil.Value <= DateTime.UtcNow)
        {
            customer.IsBotPaused = false;
            customer.BotPausedUntil = null;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Bot auto-resumed for customer {CustomerId} (pause expired)", customer.Id);
            return false;
        }

        return true;
    }
}
