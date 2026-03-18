using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Persists admin notifications to the database and pushes them to connected admins via SignalR.
/// This ensures notifications survive server restarts and admin logouts.
/// </summary>
public class AdminNotificationService : IAdminNotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<AdminNotificationService> _logger;

    private const int MaxUnread = 50;

    public AdminNotificationService(AppDbContext db, IHubContext<NotificationHub> hubContext, ILogger<AdminNotificationService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CreateAndPushAsync(int orderId, string orderNumber, string customerName, decimal amount, string status, CancellationToken ct = default)
    {
        // 1. Persist to database
        var notification = new AdminNotification
        {
            OrderId = orderId,
            OrderNumber = orderNumber,
            CustomerName = customerName,
            Amount = amount,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.AdminNotifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        // 2. Push to connected admins via SignalR (best-effort — DB is source of truth)
        try
        {
            await _hubContext.Clients.Group("admins").SendAsync("NewOrder", new OrderNotificationDto
            {
                Id = notification.Id,
                OrderId = orderId,
                OrderNumber = orderNumber,
                CustomerName = customerName,
                Amount = amount,
                Timestamp = notification.CreatedAt,
                Status = status
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push SignalR notification for order {OrderNumber}", orderNumber);
        }
    }

    /// <inheritdoc />
    public async Task<List<OrderNotificationDto>> GetUnreadAsync(CancellationToken ct = default)
    {
        return await _db.AdminNotifications
            .Where(n => !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(MaxUnread)
            .Select(n => new OrderNotificationDto
            {
                Id = n.Id,
                OrderId = n.OrderId,
                OrderNumber = n.OrderNumber,
                CustomerName = n.CustomerName,
                Amount = n.Amount,
                Timestamp = n.CreatedAt,
                Status = n.Status
            })
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(int notificationId, CancellationToken ct = default)
    {
        await _db.AdminNotifications
            .Where(n => n.Id == notificationId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    /// <inheritdoc />
    public async Task MarkAllAsReadAsync(CancellationToken ct = default)
    {
        await _db.AdminNotifications
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
