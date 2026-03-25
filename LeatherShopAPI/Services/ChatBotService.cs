using LeatherShopAPI.Models;
using LeatherShopAPI.Services.ChatBot;
using LeatherShopAPI.Services.ChatBot.Handlers;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Thin router/orchestrator for the WhatsApp chatbot conversation flow.
/// Delegates to specialized handler classes for each domain (menu, products, cart, checkout, orders).
/// </summary>
public class ChatBotService : IChatBotService
{
    private readonly BotMessageSender _bot;
    private readonly ConversationStateService _convState;
    private readonly MenuHandler _menuHandler;
    private readonly ProductHandler _productHandler;
    private readonly CartHandler _cartHandler;
    private readonly CheckoutHandler _checkoutHandler;
    private readonly OrderHistoryHandler _orderHistoryHandler;
    private readonly OrderCancellationHandler _orderCancellationHandler;
    private readonly ContactHandler _contactHandler;
    private readonly ILogger<ChatBotService> _logger;

    public ChatBotService(
        BotMessageSender bot,
        ConversationStateService convState,
        MenuHandler menuHandler,
        ProductHandler productHandler,
        CartHandler cartHandler,
        CheckoutHandler checkoutHandler,
        OrderHistoryHandler orderHistoryHandler,
        OrderCancellationHandler orderCancellationHandler,
        ContactHandler contactHandler,
        ILogger<ChatBotService> logger)
    {
        _bot = bot;
        _convState = convState;
        _menuHandler = menuHandler;
        _productHandler = productHandler;
        _cartHandler = cartHandler;
        _checkoutHandler = checkoutHandler;
        _orderHistoryHandler = orderHistoryHandler;
        _orderCancellationHandler = orderCancellationHandler;
        _contactHandler = contactHandler;
        _logger = logger;
    }

    /// <summary>Process an incoming WhatsApp message and route to the appropriate handler.</summary>
    public async Task ProcessMessage(Customer customer, string messageType,
        string? textBody, string? interactiveId, string? interactiveTitle, CancellationToken ct = default)
    {
        var phone = customer.PhoneNumber;

        // Set per-request customer ID for bot message saving
        _bot.CurrentCustomerId = customer.Id;

        var input = (interactiveId ?? textBody ?? "").Trim().ToLower();

        try
        {
            // --- PENDING STATE HANDLERS (address confirmation, address input, quantity) ---
            if (await HandlePendingStates(phone, customer, input, textBody, interactiveId, ct))
                return;

            // --- ROUTE BY INPUT ---
            await RouteInput(phone, customer, input, ct);
        }
        catch (WhatsAppApiException ex)
        {
            _logger.LogError(ex, "WhatsApp API error processing message from {Phone}", phone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {Phone}", phone);
            try
            {
                await _bot.SendText(phone, "Sorry, something went wrong. Please type *menu* to start again.", ct);
            }
            catch (Exception sendEx)
            {
                _logger.LogWarning(sendEx, "Failed to send error message to {Phone}", phone);
            }
        }
    }

    /// <summary>
    /// Handles pending state flows (address confirmation, address input, quantity input).
    /// Returns true if a pending state was handled (caller should return).
    /// </summary>
    private async Task<bool> HandlePendingStates(string phone, Customer customer,
        string input, string? textBody, string? interactiveId, CancellationToken ct)
    {
        var state = _convState.GetState(customer.Id);

        // ---- ADDRESS CONFIRMATION ----
        if (state.PendingAction == ConversationState.PendingActions.ConfirmingAddress)
        {
            if (interactiveId == "confirm_address")
            {
                _convState.SetPendingAction(customer.Id, null);
                await _checkoutHandler.PlaceOrder(phone, customer, ct);
                return true;
            }
            if (interactiveId == "change_address")
            {
                _convState.SetPendingAction(customer.Id, ConversationState.PendingActions.AwaitingAddress);
                await _bot.SendText(phone,
                    "📍 *Enter your new shipping address:*\n\n" +
                    "_Example: 123, MG Road, Anna Nagar, Chennai - 600040_", ct);
                return true;
            }
            if (!string.IsNullOrEmpty(interactiveId))
            {
                _convState.SetPendingAction(customer.Id, null);
                // Fall through to normal routing
            }
            else
            {
                await _bot.SendText(phone, "Please tap *✅ Confirm* or *✏️ Change Address* above.", ct);
                return true;
            }
        }

        // ---- ADDRESS INPUT ----
        if (state.PendingAction == ConversationState.PendingActions.AwaitingAddress)
        {
            if (!string.IsNullOrEmpty(interactiveId))
            {
                _convState.SetPendingAction(customer.Id, null);
                // Fall through to normal routing
            }
            else
            {
                var rawAddress = (textBody ?? "").Trim();
                if (rawAddress.Length >= 10)
                {
                    await _checkoutHandler.HandleAddressInput(phone, customer, rawAddress, ct);
                    return true;
                }
                await _bot.SendText(phone, "📍 That seems too short. Please enter your *full shipping address* (at least 10 characters):\n\nExample: _123, MG Road, Anna Nagar, Chennai - 600040_", ct);
                return true;
            }
        }

        // ---- QUANTITY INPUT ----
        if (state.PendingProductId.HasValue && int.TryParse(input, out var qty))
        {
            await _cartHandler.AddToCartWithQuantity(phone, customer, state.PendingProductId.Value, qty, state.PendingImageId, ct);
            return true;
        }

        // Clear stale pending product if user typed something else
        if (state.PendingProductId.HasValue)
        {
            _convState.ClearPendingProduct(customer.Id);
        }

        return false;
    }

    /// <summary>Routes the user's input to the appropriate handler.</summary>
    private async Task RouteInput(string phone, Customer customer, string input, CancellationToken ct)
    {
        // Stale checkout button replies (confirm_address / change_address) that arrive
        // after the state was already cleared (e.g. user tapped both buttons, or a
        // duplicate webhook delivery). Silently ignore to avoid the default "Welcome" response.
        if (input is "confirm_address" or "change_address")
            return;

        // Main menu
        if (input is "hi" or "hello" or "hey" or "menu" or "start" or "main_menu")
        {
            await _menuHandler.SendMainMenu(phone, customer.Name, ct);
            return;
        }

        // Browse categories
        if (input == "browse_categories")
        {
            await _menuHandler.SendCategoryList(phone, ct);
            return;
        }

        // Selected a category
        if (input.StartsWith("cat_"))
        {
            var category = input["cat_".Length..].Replace("_", " ");
            if (string.IsNullOrWhiteSpace(category))
            {
                await _bot.SendText(phone, "Invalid category. Type *menu* to browse our options.", ct);
                return;
            }
            await _productHandler.SendProductsInCategory(phone, category, ct);
            return;
        }

        // Selected a product (view details)
        if (input.StartsWith("prod_"))
        {
            if (int.TryParse(input.Replace("prod_", ""), out var productId))
            {
                await _productHandler.SendProductDetails(phone, productId, ct);
                return;
            }
            await _bot.SendText(phone, "Invalid product. Type *menu* to browse.", ct);
            return;
        }

        // View product from carousel (quick_reply)
        if (input.StartsWith("view_") && input != "view_cart")
        {
            var (viewProdId, viewImgId) = ChatBotHelpers.ParseProductImagePayload(input, "view_");
            if (viewProdId.HasValue)
            {
                await _productHandler.SendProductDetailsText(phone, viewProdId.Value, viewImgId, ct);
                return;
            }
            await _bot.SendText(phone, "Invalid product. Type *menu* to browse.", ct);
            return;
        }

        // Add to cart (ask for quantity)
        if (input.StartsWith("addcart_"))
        {
            var (cartProdId, cartImgId) = ChatBotHelpers.ParseProductImagePayload(input, "addcart_");
            if (cartProdId.HasValue)
            {
                await _cartHandler.AskQuantity(phone, customer, cartProdId.Value, cartImgId, ct);
                return;
            }
            await _bot.SendText(phone, "Invalid product. Type *menu* to browse.", ct);
            return;
        }

        // View cart
        if (input == "view_cart")
        {
            await _cartHandler.SendCartSummary(phone, customer.Id, ct);
            return;
        }

        // Edit cart (show removable item list)
        if (input == "edit_cart")
        {
            await _cartHandler.SendEditCartList(phone, customer.Id, ct);
            return;
        }

        // Remove individual cart item (list row id: rmcart_{cartItemId})
        if (input.StartsWith("rmcart_"))
        {
            if (int.TryParse(input["rmcart_".Length..], out var cartItemId))
            {
                await _cartHandler.RemoveCartItem(phone, customer.Id, cartItemId, ct);
                return;
            }
            await _bot.SendText(phone, "Invalid item. Type *menu* to start again.", ct);
            return;
        }

        // Clear cart
        if (input == "clear_cart")
        {
            await _cartHandler.ClearCart(phone, customer.Id, ct);
            return;
        }

        // Checkout
        if (input == "checkout")
        {
            await _checkoutHandler.ProcessCheckout(phone, customer, ct);
            return;
        }

        // My orders
        if (input == "my_orders")
        {
            await _orderHistoryHandler.SendOrderHistory(phone, customer.Id, ct);
            return;
        }

        // Customer-initiated order cancellation (button id: cancel_ord_{orderId})
        if (input.StartsWith("cancel_ord_"))
        {
            var suffix = input["cancel_ord_".Length..];
            if (int.TryParse(suffix, out var orderId))
            {
                await _orderCancellationHandler.HandleCancelOrder(phone, customer, orderId, ct);
                return;
            }
            // Malformed suffix — silently ignore rather than showing a confusing default response
            _logger.LogWarning("Received malformed cancel_ord_ button id '{Input}' from {Phone}.", input, phone);
            await _bot.SendText(phone, "❌ Invalid order reference. Type *my orders* to view your orders.", ct);
            return;
        }

        // Contact Us
        if (input == "contact_us")
        {
            await _contactHandler.SendContactInfo(phone, ct);
            return;
        }

        // Default: show main menu
        await _bot.SendText(phone, "🙏 Welcome to Cuir Galerie! Type *menu* to see options.", ct);
        await _menuHandler.SendMainMenu(phone, customer.Name, ct);
    }
}
