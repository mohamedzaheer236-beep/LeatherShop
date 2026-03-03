using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services;

/// <summary>
/// Background service that runs every 60 seconds to find unpaid orders past their
/// PaymentExpiresAt deadline. For each expired order it:
///   1. Cancels the order
///   2. Restores product stock
///   3. Restores cart items so the customer can re-checkout
///
/// This ensures stock isn't permanently locked by abandoned orders — even if the
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
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Find all pending (unpaid) orders that have expired
        var expiredOrders = await db.Orders
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .Where(o => o.PaymentExpiresAt != null
                     && o.PaymentExpiresAt < now
                     && !o.IsPaid
                     && o.Status == OrderStatus.Pending)
            .ToListAsync(ct);

        if (expiredOrders.Count == 0) return;

        _logger.LogInformation("Found {Count} expired unpaid order(s) to clean up.", expiredOrders.Count);

        foreach (var order in expiredOrders)
        {
            // 1. Cancel the order
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = now;

            // 2. Restore stock
            foreach (var item in order.OrderItems)
            {
                item.Product.StockQuantity += item.Quantity;
            }

            // 3. Restore cart items (merge with any existing)
            var existingCartItems = await db.CartItems
                .Where(ci => ci.CustomerId == order.CustomerId)
                .ToListAsync(ct);

            foreach (var orderItem in order.OrderItems)
            {
                var existingCart = existingCartItems
                    .FirstOrDefault(ci => ci.ProductId == orderItem.ProductId && ci.SelectedImageId == orderItem.SelectedImageId);

                if (existingCart != null)
                {
                    existingCart.Quantity += orderItem.Quantity;
                }
                else
                {
                    var newCartItem = new CartItem
                    {
                        CustomerId = order.CustomerId,
                        ProductId = orderItem.ProductId,
                        Quantity = orderItem.Quantity,
                        SelectedImageId = orderItem.SelectedImageId
                    };
                    db.CartItems.Add(newCartItem);
                    existingCartItems.Add(newCartItem); // Track to avoid duplicates within same batch
                }
            }

            _logger.LogInformation("Expired order {OrderNumber}: cancelled, stock restored, cart restored for customer {CustomerId}.",
                order.OrderNumber, order.CustomerId);
        }

        await db.SaveChangesAsync(ct);
    }
}
