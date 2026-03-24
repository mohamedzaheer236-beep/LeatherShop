using LeatherShopAPI.Models.WhatsApp;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services.ChatBot.Handlers;

/// <summary>
/// Handles customer-initiated order cancellation via WhatsApp chatbot.
/// Single entry point for all three surfaces that expose a cancel button
/// (Order History, TryShowPendingOrder, Cart Summary — pending order path).
/// </summary>
public class OrderCancellationHandler
{
    private readonly IOrderService _orderService;
    private readonly BotMessageSender _bot;
    private readonly ILogger<OrderCancellationHandler> _logger;

    public OrderCancellationHandler(
        IOrderService orderService,
        BotMessageSender bot,
        ILogger<OrderCancellationHandler> logger)
    {
        _orderService = orderService;
        _bot = bot;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to cancel the given order for this customer and sends the appropriate
    /// WhatsApp response regardless of outcome.
    /// </summary>
    public async Task HandleCancelOrder(string to, Customer customer, int orderId, CancellationToken ct = default)
    {
        var result = await _orderService.CancelByCustomerAsync(orderId, customer.Id, ct);

        switch (result)
        {
            case CancelOrderResult.Success:
                _logger.LogInformation("Customer {CustomerId} successfully cancelled order {OrderId}.", customer.Id, orderId);
                await _bot.SendButtons(to,
                    "✅ *Order Cancelled*\n\n" +
                    "Your order has been cancelled and your items have been returned to your cart. " +
                    "You can re-order at any time.",
                    new List<ButtonOption>
                    {
                        new() { Id = "view_cart",         Title = "🛒 View Cart" },
                        new() { Id = "browse_categories", Title = "🛍️ Browse" },
                        new() { Id = "main_menu",         Title = "🏠 Menu" }
                    },
                    ct: ct);
                break;

            case CancelOrderResult.NotFound:
                // Could be a legitimate not-found, or a security mismatch — treat both the same
                await _bot.SendText(to,
                    "❌ We couldn't find that order. It may have already been cancelled or the link has expired.\n\n" +
                    "Type *my orders* to check your order history.", ct);
                break;

            case CancelOrderResult.NotCancellable:
                await _bot.SendText(to,
                    "❌ *This order cannot be cancelled.*\n\n" +
                    "Orders that have already been paid or are being processed cannot be cancelled through WhatsApp. " +
                    "Please contact us directly if you need help.", ct);
                break;

            case CancelOrderResult.ConcurrencyConflict:
                await _bot.SendText(to,
                    "⚠️ We couldn't process the cancellation right now due to a brief conflict. " +
                    "Please type *my orders* and try cancelling again.", ct);
                break;

            default:
                _logger.LogError("Unhandled CancelOrderResult {Result} for order {OrderId}, customer {CustomerId}.", result, orderId, customer.Id);
                await _bot.SendText(to,
                    "⚠️ Something went wrong. Please type *menu* to start again.", ct);
                break;
        }
    }
}
