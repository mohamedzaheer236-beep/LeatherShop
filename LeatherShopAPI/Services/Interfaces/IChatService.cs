using System.Threading;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface IChatService
{
    /// <summary>Get paginated list of customer conversations (ordered by last message).</summary>
    Task<List<ConversationDto>> GetConversationsAsync(string? search, CancellationToken ct = default);

    /// <summary>Get paginated chat messages for a specific customer.</summary>
    Task<PaginatedResult<ChatMessageDto>> GetMessagesAsync(int customerId, int page = 1, int pageSize = 50, CancellationToken ct = default);

    /// <summary>Admin sends a WhatsApp message to a customer (auto-pauses bot for 30 min).</summary>
    Task<ChatMessageDto> SendMessageAsync(int customerId, string text, CancellationToken ct = default);

    /// <summary>Toggle bot pause state for a customer. Returns null if customer not found.</summary>
    Task<bool?> ToggleBotAsync(int customerId, CancellationToken ct = default);

    /// <summary>Save an incoming or outgoing message to the database.</summary>
    Task<ChatMessage> SaveMessageAsync(int customerId, MessageDirection direction, string content,
        string senderName, bool isFromBot, string messageType = "text", CancellationToken ct = default);

    /// <summary>Check if the bot is currently paused for a customer.</summary>
    Task<bool> IsBotPausedAsync(int customerId, CancellationToken ct = default);

    /// <summary>Delete all chat messages for a customer.</summary>
    Task<bool> DeleteConversationAsync(int customerId, CancellationToken ct = default);

    /// <summary>Get all permanently failed outbox messages (for admin follow-up).</summary>
    Task<List<FailedOutboxMessageDto>> GetFailedOutboxMessagesAsync(CancellationToken ct = default);

    /// <summary>Retry a permanently failed outbox message (resets to Pending). Returns false if not found or not Failed.</summary>
    Task<bool> RetryOutboxMessageAsync(int outboxMessageId, CancellationToken ct = default);

    /// <summary>Get count of failed outbox messages (for badge display).</summary>
    Task<int> GetFailedOutboxCountAsync(CancellationToken ct = default);
}
