using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using LeatherShopAPI.Services;
using LeatherShopAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeatherShopAPI.Services.ChatBot.Handlers;

/// <summary>
/// Handles checkout flow: address confirmation, address input, and order placement.
/// </summary>
public class CheckoutHandler
{
    private readonly AppDbContext _db;
    private readonly BotMessageSender _bot;
    private readonly ConversationStateService _convState;
    private readonly IAdminNotificationService _adminNotifications;
    private readonly IConfiguration _config;
    private readonly ILogger<CheckoutHandler> _logger;

    public CheckoutHandler(AppDbContext db, BotMessageSender bot, ConversationStateService convState,
        IAdminNotificationService adminNotifications, IConfiguration config, ILogger<CheckoutHandler> logger)
    {
        _db = db;
        _bot = bot;
        _convState = convState;
        _adminNotifications = adminNotifications;
        _config = config;
        _logger = logger;
    }

    public async Task ProcessCheckout(string to, Customer customer, CancellationToken ct = default)
    {
        var cartItems = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CustomerId == customer.Id)
            .ToListAsync(ct);

        if (!cartItems.Any())
        {
            if (await TryShowPendingOrder(to, customer.Id, ct))
                return;

            await _bot.SendText(to, "🛒 Your cart is empty! Browse products first.", ct);
            return;
        }

        // Check stock availability (aggregate per product to handle multi-image-selection carts)
        var stockNeeded = cartItems
            .GroupBy(ci => ci.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(ci => ci.Quantity));

        foreach (var (productId, totalNeeded) in stockNeeded)
        {
            var product = cartItems.First(ci => ci.ProductId == productId).Product;
            if (product.StockQuantity < totalNeeded)
            {
                await _bot.SendText(to, $"❌ Sorry, *{product.Name}* only has {product.StockQuantity} left in stock. Please update your cart.", ct);
                return;
            }
        }

        // Ask for shipping address if not set
        if (string.IsNullOrWhiteSpace(customer.Address))
        {
            _convState.SetPendingAction(customer.Id, ConversationState.PendingActions.AwaitingAddress);

            await _bot.SendText(to,
                "📍 *Shipping Address Required*\n\n" +
                "Please type your full shipping address:\n\n" +
                "_Example: 123, MG Road, Anna Nagar, Chennai - 600040_", ct);
            return;
        }

        // Address exists - ask the customer to confirm or change it
        _convState.SetPendingAction(customer.Id, ConversationState.PendingActions.ConfirmingAddress);

        var cartTotal = cartItems.Sum(ci => ci.Product.Price * ci.Quantity);
        var itemLines = string.Join("\n", cartItems.Select(ci =>
            $"  • {ci.Product.Name} x{ci.Quantity} - ₹{ci.Product.Price * ci.Quantity}"));

        await _bot.SendButtons(to,
            $"📋 *Order Summary*\n\n" +
            $"{itemLines}\n" +
            $"💰 Total: *₹{cartTotal}*\n\n" +
            $"📍 *Shipping to:*\n_{customer.Address}_\n\n" +
            $"Is this address correct?",
            new List<ButtonOption>
            {
                new() { Id = "confirm_address", Title = "✅ Confirm" },
                new() { Id = "change_address", Title = "✏️ Change Address" }
            },
            ct: ct);
    }

    public async Task PlaceOrder(string to, Customer customer, CancellationToken ct = default)
    {
        var cartItems = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CustomerId == customer.Id)
            .ToListAsync(ct);

        if (!cartItems.Any())
        {
            if (await TryShowPendingOrder(to, customer.Id, ct))
                return;

            await _bot.SendText(to, "🛒 Your cart is empty! Browse products first.", ct);
            return;
        }

        // Re-check stock (aggregate per product to handle multi-image-selection carts)
        var stockNeeded = cartItems
            .GroupBy(ci => ci.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(ci => ci.Quantity));

        foreach (var (productId, totalNeeded) in stockNeeded)
        {
            var product = cartItems.First(ci => ci.ProductId == productId).Product;
            if (product.StockQuantity < totalNeeded)
            {
                await _bot.SendText(to, $"❌ Sorry, *{product.Name}* only has {product.StockQuantity} left in stock (you need {totalNeeded}). Please update your cart.", ct);
                return;
            }
        }

        // Pre-flight: verify we can generate a payment link BEFORE creating the order
        var baseUrl = ChatBotHelpers.GetPublicBaseUrl(_config);
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.LogError("Cannot generate payment link - App:BaseUrl is not configured and RAILWAY_PUBLIC_DOMAIN is not set");
            await _bot.SendText(to, "❌ Sorry, we couldn't generate a payment link right now. Please contact us directly to complete your order.", ct);
            _convState.SetPendingAction(customer.Id, null);
            return;
        }

        // Create order
        decimal total = cartItems.Sum(ci => ci.Product.Price * ci.Quantity);
        var order = new Order
        {
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            CustomerId = customer.Id,
            TotalAmount = total,
            Status = OrderStatus.Pending,
            ShippingAddress = customer.Address,
            PaymentExpiresAt = DateTime.UtcNow.AddMinutes(5),
            OrderItems = cartItems.Select(ci => new OrderItem
            {
                ProductId = ci.ProductId,
                Quantity = ci.Quantity,
                UnitPrice = ci.Product.Price,
                SelectedImageId = ci.SelectedImageId
            }).ToList()
        };

        _db.Orders.Add(order);

        // Reduce stock
        foreach (var item in cartItems)
        {
            item.Product.StockQuantity -= item.Quantity;
        }

        // Clear cart
        _db.CartItems.RemoveRange(cartItems);

        var paymentUrl = $"{baseUrl}/api/payment/pay/{Uri.EscapeDataString(order.OrderNumber)}";

        var orderSummary = $"✅ *Order Placed!*\n\n" +
                           $"📋 Order: *{order.OrderNumber}*\n" +
                           $"💰 Total: *₹{total}*\n" +
                           $"📍 Ship to: _{customer.Address}_\n\n" +
                           $"💳 Pay here: {paymentUrl}\n\n" +
                           $"⏳ This link expires in *5 minutes*.\n" +
                           $"If it expires, just say *checkout* to get a new link.\n\n" +
                           $"We'll confirm once payment is received.";

        // Transactional Outbox: write the WhatsApp message to the DB in the SAME transaction.
        // Set NextRetryAt to 30s from now so the background processor doesn't pick it up
        // while we attempt the immediate inline send below.
        var outboxMessage = new WhatsAppOutboxMessage
        {
            To = to,
            Content = orderSummary,
            Context = $"Order confirmation for {order.OrderNumber}",
            Status = OutboxMessageStatus.Pending,
            NextRetryAt = DateTime.UtcNow.AddSeconds(30)
        };
        _db.WhatsAppOutboxMessages.Add(outboxMessage);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Stock concurrency conflict while placing order for customer {CustomerId}.", customer.Id);
            foreach (var entry in _db.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
            await _bot.SendText(to, "⚠️ Sorry, another order was placed at the same time for the same product. Please try placing your order again - your cart is still intact.", ct);
            return;
        }

        // Persist notification + push to connected admins via SignalR
        try
        {
            await _adminNotifications.CreateAndPushAsync(
                order.Id, order.OrderNumber,
                string.IsNullOrEmpty(customer.Name) ? customer.PhoneNumber : customer.Name,
                order.TotalAmount, "Pending", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create notification for {OrderNumber}", order.OrderNumber);
        }

        // Try to send immediately (fast path)
        try
        {
            await _bot.SendText(to, orderSummary, ct);

            outboxMessage.Status = OutboxMessageStatus.Sent;
            outboxMessage.SentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (WhatsAppApiException ex)
        {
            _logger.LogWarning(ex,
                "Immediate send failed for {OrderNumber} to {Phone} - outbox message {OutboxId} will be retried by background processor",
                order.OrderNumber, to, outboxMessage.Id);
        }
    }

    public async Task HandleAddressInput(string to, Customer customer, string address, CancellationToken ct = default)
    {
        customer.Address = address;
        _convState.SetPendingAction(customer.Id, null);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _bot.SendText(to, $"✅ Address saved:\n_{address}_\n\nProceeding to checkout...", ct);
        }
        catch (WhatsAppApiException ex)
        {
            _logger.LogWarning(ex, "Failed to send address confirmation to {Phone}, continuing to place order", to);
        }

        await PlaceOrder(to, customer, ct);
    }

    private async Task<bool> TryShowPendingOrder(string to, int customerId, CancellationToken ct = default)
    {
        var pendingOrder = await _db.Orders
            .Where(o => o.CustomerId == customerId
                     && o.Status == OrderStatus.Pending
                     && o.PaymentExpiresAt != null
                     && o.PaymentExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (pendingOrder == null) return false;

        var baseUrl = ChatBotHelpers.GetPublicBaseUrl(_config);
        var paymentUrl = baseUrl != null
            ? $"{baseUrl}/api/payment/pay/{Uri.EscapeDataString(pendingOrder.OrderNumber)}"
            : null;

        await _bot.SendButtons(to,
            $"⏳ You already have a pending order *{pendingOrder.OrderNumber}* (₹{pendingOrder.TotalAmount}).\n\n" +
            (paymentUrl != null ? $"💳 Pay here: {paymentUrl}\n\n" : "") +
            $"Complete the payment first, or cancel the order below to start a new one.",
            new List<ButtonOption>
            {
                new() { Id = $"cancel_ord_{pendingOrder.Id}", Title = "❌ Cancel Order" },
                new() { Id = "main_menu",                     Title = "🏠 Main Menu" }
            },
            ct: ct);
        return true;
    }
}
