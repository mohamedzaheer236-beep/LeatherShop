using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Helpers;

/// <summary>
/// Shared logic for cancelling an expired/abandoned order and restoring
/// product stock + customer cart items. Used by both <see cref="Services.PaymentService"/>
/// and <see cref="Services.ExpiredOrderCleanupService"/> to avoid duplication.
/// </summary>
internal static class OrderExpiryHelper
{
    /// <summary>
    /// Cancels the order, restores product stock, and merges order items back into
    /// the customer's cart so they can re-checkout without re-adding products.
    /// <para>
    /// <b>Does NOT call SaveChangesAsync</b> - the caller is responsible for persisting changes.
    /// This allows batch operations (e.g., cleaning up multiple orders) to commit once.
    /// </para>
    /// </summary>
    /// <param name="cancelledBy">Who initiated the cancellation: "Customer", "Admin", or "System".</param>
    internal static async Task CancelAndRestoreCartAsync(AppDbContext db, Order order, string cancelledBy, ILogger? logger = null)
    {
        if (order.Status == OrderStatus.Cancelled) return;

        if (order.OrderItems == null || !order.OrderItems.Any())
            throw new InvalidOperationException(
                $"Order {order.OrderNumber} must be loaded with .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).");

        logger?.LogInformation(
            "Cancelling order {OrderNumber} (CancelledBy={CancelledBy}) and restoring cart for customer {CustomerId}.",
            order.OrderNumber, cancelledBy, order.CustomerId);

        // 1. Cancel the order
        order.Status = OrderStatus.Cancelled;
        order.CancelledBy = cancelledBy;
        order.UpdatedAt = DateTime.UtcNow;

        // 2. Restore stock
        foreach (var item in order.OrderItems)
        {
            if (item.Product == null)
                throw new InvalidOperationException(
                    $"OrderItem {item.Id} missing Product navigation - ensure .ThenInclude(oi => oi.Product).");
            item.Product.StockQuantity += item.Quantity;
        }

        // 3. Restore cart items (merge with any existing)
        var existingCartItems = await db.CartItems
            .Where(ci => ci.CustomerId == order.CustomerId)
            .ToListAsync();

        foreach (var orderItem in order.OrderItems)
        {
            var existingCart = existingCartItems
                .FirstOrDefault(ci => ci.ProductId == orderItem.ProductId
                                   && ci.SelectedImageId == orderItem.SelectedImageId);

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
    }
}
