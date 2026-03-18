using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiVersion("1.0")]
[Authorize]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IAdminNotificationService _notificationService;

    public NotificationsController(IAdminNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Get unread admin notifications (max 50, most recent first).</summary>
    [HttpGet("unread")]
    public async Task<IActionResult> GetUnread(CancellationToken ct)
    {
        var notifications = await _notificationService.GetUnreadAsync(ct);
        return Ok(ApiResponse<List<OrderNotificationDto>>.Ok(notifications));
    }

    /// <summary>Mark a single notification as read.</summary>
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken ct)
    {
        await _notificationService.MarkAsReadAsync(id, ct);
        return Ok(ApiResponse.Ok("Notification marked as read"));
    }

    /// <summary>Mark all notifications as read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        await _notificationService.MarkAllAsReadAsync(ct);
        return Ok(ApiResponse.Ok("All notifications marked as read"));
    }
}
