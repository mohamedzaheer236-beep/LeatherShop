using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface IChatService
{
    /// <summary>Get paginated list of customer conversations (ordered by last message).</summary>
    Task<List<ConversationDto>> GetConversationsAsync(string? search);

    /// <summary>Get paginated chat messages for a specific customer.</summary>
    Task<PaginatedResult<ChatMessageDto>> GetMessagesAsync(int customerId, int page = 1, int pageSize = 50);

    /// <summary>Admin sends a WhatsApp message to a customer (auto-pauses bot for 30 min).</summary>
    Task<ChatMessageDto> SendMessageAsync(int customerId, string text);

    /// <summary>Toggle bot pause state for a customer. Returns null if customer not found.</summary>
    Task<bool?> ToggleBotAsync(int customerId);

    /// <summary>Save an incoming or outgoing message to the database.</summary>
    Task<ChatMessage> SaveMessageAsync(int customerId, MessageDirection direction, string content,
        string senderName, bool isFromBot, string messageType = "text");

    /// <summary>Check if the bot is currently paused for a customer.</summary>
    Task<bool> IsBotPausedAsync(int customerId);

    /// <summary>Delete all chat messages for a customer.</summary>
    Task<bool> DeleteConversationAsync(int customerId);
}
