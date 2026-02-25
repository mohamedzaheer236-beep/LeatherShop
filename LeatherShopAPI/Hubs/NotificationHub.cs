using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LeatherShopAPI.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard notifications:
///  - New order alerts (pushed to all connected admins)
///  - Chat messages (pushed to admins viewing a specific customer's chat)
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>Join a group to receive chat messages for a specific customer.</summary>
    public async Task JoinCustomerChat(int customerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{customerId}");
        _logger.LogInformation("Admin {ConnId} joined chat group for customer {CustomerId}",
            Context.ConnectionId, customerId);
    }

    /// <summary>Leave the customer chat group.</summary>
    public async Task LeaveCustomerChat(int customerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{customerId}");
        _logger.LogInformation("Admin {ConnId} left chat group for customer {CustomerId}",
            Context.ConnectionId, customerId);
    }

    public override async Task OnConnectedAsync()
    {
        // All authenticated admins join the "admins" group for broadcast notifications
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        _logger.LogInformation("Admin connected to NotificationHub: {ConnId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");
        _logger.LogInformation("Admin disconnected from NotificationHub: {ConnId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
