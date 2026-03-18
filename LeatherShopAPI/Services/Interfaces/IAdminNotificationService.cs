using LeatherShopAPI.DTOs.Chat;

namespace LeatherShopAPI.Services.Interfaces;

/// <summary>
/// Manages persistent admin notifications for order lifecycle events.
/// Persists to DB and pushes real-time via SignalR.
/// </summary>
public interface IAdminNotificationService
{
    /// <summary>Persist a notification and push it to connected admins via SignalR.</summary>
    Task CreateAndPushAsync(int orderId, string orderNumber, string customerName, decimal amount, string status, CancellationToken ct = default);

    /// <summary>Get the most recent unread notifications (capped at 50).</summary>
    Task<List<OrderNotificationDto>> GetUnreadAsync(CancellationToken ct = default);

    /// <summary>Mark a single notification as read.</summary>
    Task MarkAsReadAsync(int notificationId, CancellationToken ct = default);

    /// <summary>Mark all unread notifications as read.</summary>
    Task MarkAllAsReadAsync(CancellationToken ct = default);
}
