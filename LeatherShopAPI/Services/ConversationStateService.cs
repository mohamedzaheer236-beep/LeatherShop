using Microsoft.Extensions.Caching.Memory;

namespace LeatherShopAPI.Services;

/// <summary>
/// Manages ephemeral chatbot conversation state (pending product, pending image, pending action)
/// using in-memory cache instead of persisting to the database on every interaction.
/// State auto-expires after 30 minutes of inactivity.
/// Suitable for single-replica deployments (Railway).
/// </summary>
public class ConversationStateService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);

    public ConversationStateService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>Gets the current conversation state for a customer, or empty state if none exists.</summary>
    public ConversationState GetState(int customerId)
        => _cache.TryGetValue(CacheKey(customerId), out ConversationState? state) && state != null
            ? state
            : new ConversationState();

    /// <summary>Sets conversation state for a customer with sliding expiration.</summary>
    public void SetState(int customerId, ConversationState state)
        => _cache.Set(CacheKey(customerId), state, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration
        });

    /// <summary>Sets the pending product (awaiting quantity input).</summary>
    public void SetPendingProduct(int customerId, int productId, int? imageId = null)
    {
        var state = GetState(customerId);
        state.PendingProductId = productId;
        state.PendingImageId = imageId;
        SetState(customerId, state);
    }

    /// <summary>Clears the pending product state.</summary>
    public void ClearPendingProduct(int customerId)
    {
        var state = GetState(customerId);
        state.PendingProductId = null;
        state.PendingImageId = null;
        SetState(customerId, state);
    }

    /// <summary>Sets the pending action (e.g. awaiting_address, confirming_address).</summary>
    public void SetPendingAction(int customerId, string? action)
    {
        var state = GetState(customerId);
        state.PendingAction = action;
        SetState(customerId, state);
    }

    /// <summary>Clears all conversation state for a customer.</summary>
    public void ClearState(int customerId)
        => _cache.Remove(CacheKey(customerId));

    private static string CacheKey(int customerId) => $"conv_state_{customerId}";
}

/// <summary>Ephemeral chatbot conversation state — lives in memory, not in the database.</summary>
public class ConversationState
{
    /// <summary>When set, the bot is waiting for the customer to type a quantity for this product.</summary>
    public int? PendingProductId { get; set; }

    /// <summary>Temporarily stores the selected ProductImage ID while the bot asks for quantity.</summary>
    public int? PendingImageId { get; set; }

    /// <summary>Tracks a pending bot action, e.g. "awaiting_address". Null when idle.</summary>
    public string? PendingAction { get; set; }

    /// <summary>Well-known PendingAction values used by the chatbot.</summary>
    public static class PendingActions
    {
        public const string AwaitingAddress = "awaiting_address";
        public const string ConfirmingAddress = "confirming_address";
    }
}
