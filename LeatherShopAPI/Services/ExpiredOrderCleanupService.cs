using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.Helpers;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Background service that runs every 60 seconds to find unpaid orders past their
/// PaymentExpiresAt deadline. For each expired order it:
///   1. Cancels the order
///   2. Restores product stock
///   3. Restores cart items so the customer can re-checkout
///
/// This ensures stock isn't permanently locked by abandoned orders - even if the
/// customer never revisits the expired payment link.
/// </summary>
public sealed class ExpiredOrderCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpiredOrderCleanupService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    public ExpiredOrderCleanupService(IServiceProvider serviceProvider, ILogger<ExpiredOrderCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredOrderCleanupService started. Polling every {Seconds}s for expired unpaid orders.",
            PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredOrdersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in ExpiredOrderCleanupService cycle");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task CleanupExpiredOrdersAsync(CancellationToken ct)
    {
        // First, query for expired order IDs using a short-lived context
        List<int> expiredOrderIds;
        using (var queryScope = _serviceProvider.CreateScope())
        {
            var queryDb = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();
            expiredOrderIds = await queryDb.Orders
                .Where(o => o.PaymentExpiresAt != null
                         && o.PaymentExpiresAt < DateTime.UtcNow
                         && !o.IsPaid
                         && o.Status == OrderStatus.Pending)
                .Select(o => o.Id)
                .ToListAsync(ct);
        }

        if (expiredOrderIds.Count == 0) return;

        _logger.LogInformation("Found {Count} expired unpaid order(s) to clean up.", expiredOrderIds.Count);

        // Process each order in its own scope so one failure doesn't block others
        foreach (var orderId in expiredOrderIds)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var order = await db.Orders
                    .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.Status == OrderStatus.Pending && !o.IsPaid, ct);

                if (order == null) continue; // Already processed or paid in the meantime

                await OrderExpiryHelper.CancelAndRestoreCartAsync(db, order, _logger);
                await db.SaveChangesAsync(ct);

                // Persist notification + push to connected admins
                try
                {
                    var adminNotifications = scope.ServiceProvider.GetRequiredService<IAdminNotificationService>();
                    await adminNotifications.CreateAndPushAsync(
                        order.Id, order.OrderNumber, "System",
                        order.TotalAmount, "Cancelled", ct);
                }
                catch (Exception hubEx)
                {
                    _logger.LogWarning(hubEx, "Failed to create notification for expired order {OrderId}", orderId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to clean up expired order {OrderId}. Will retry next cycle.", orderId);
            }
        }
    }
}
