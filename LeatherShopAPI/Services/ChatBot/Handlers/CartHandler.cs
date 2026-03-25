using System.Text;
using LeatherShopAPI.Data;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using LeatherShopAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace LeatherShopAPI.Services.ChatBot.Handlers;

/// <summary>
/// Handles cart operations: add to cart, view cart, edit cart, remove item, clear cart, quantity input.
/// </summary>
public class CartHandler
{
    private readonly AppDbContext _db;
    private readonly BotMessageSender _bot;
    private readonly ConversationStateService _convState;
    private readonly IConfiguration _config;

    public CartHandler(AppDbContext db, BotMessageSender bot, ConversationStateService convState, IConfiguration config)
    {
        _db = db;
        _bot = bot;
        _convState = convState;
        _config = config;
    }

    public async Task AskQuantity(string to, Customer customer, int productId, int? selectedImageId = null, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product == null || !product.IsActive || product.StockQuantity <= 0)
        {
            await _bot.SendText(to, "Sorry, this product is no longer available.", ct);
            return;
        }

        _convState.SetPendingProduct(customer.Id, productId, selectedImageId);

        await _bot.SendText(to,
            $"How many *{product.Name}* would you like to add?\n\n" +
            $"📦 Available: *{product.StockQuantity}*\n" +
            $"💰 Price: ₹{product.Price} each\n\n" +
            $"Type a number (e.g. *1*, *2*, *5*):", ct);
    }

    public async Task AddToCartWithQuantity(string to, Customer customer, int productId, int quantity, int? pendingImageId = null, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product == null || !product.IsActive || product.StockQuantity <= 0)
        {
            _convState.ClearPendingProduct(customer.Id);
            await _bot.SendText(to, "Sorry, this product is no longer available.", ct);
            return;
        }

        if (quantity <= 0)
        {
            await _bot.SendText(to, "❌ Please enter a valid number greater than 0.", ct);
            return;
        }

        // Stock check: sum ALL items for this product across all image selections
        var alreadyInCart = await _db.CartItems
            .Where(ci => ci.CustomerId == customer.Id && ci.ProductId == productId)
            .SumAsync(ci => ci.Quantity, ct);
        var totalNeeded = alreadyInCart + quantity;

        if (totalNeeded > product.StockQuantity)
        {
            var canAdd = product.StockQuantity - alreadyInCart;
            if (canAdd <= 0)
            {
                _convState.ClearPendingProduct(customer.Id);
                await _bot.SendButtons(to,
                    bodyText: $"❌ You already have *{alreadyInCart}* of *{product.Name}* in your cart, which is the maximum available stock.\n\nYou can't add more.",
                    buttons: new List<ButtonOption>
                    {
                        new() { Id = "view_cart", Title = "🛒 View Cart" },
                        new() { Id = "browse_categories", Title = "🛍️ Browse" },
                        new() { Id = "checkout", Title = "💳 Checkout" }
                    },
                    ct: ct);
                return;
            }

            await _bot.SendText(to,
                $"❌ Sorry, we only have *{product.StockQuantity}* of *{product.Name}* in stock." +
                (alreadyInCart > 0 ? $"\nYou already have *{alreadyInCart}* in your cart, so you can add up to *{canAdd}* more." : "") +
                $"\n\nPlease type a number between *1* and *{canAdd}*:", ct);
            return;
        }

        // Add to cart - merge only when same product AND same selected image
        var existingItem = await _db.CartItems
            .FirstOrDefaultAsync(ci => ci.CustomerId == customer.Id
                                    && ci.ProductId == productId
                                    && ci.SelectedImageId == pendingImageId, ct);

        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            _db.CartItems.Add(new CartItem
            {
                CustomerId = customer.Id,
                ProductId = productId,
                Quantity = quantity,
                SelectedImageId = pendingImageId
            });
        }

        _convState.ClearPendingProduct(customer.Id);
        await _db.SaveChangesAsync(ct);

        var cartCount = await _db.CartItems.Where(ci => ci.CustomerId == customer.Id).SumAsync(ci => ci.Quantity, ct);
        var addedSubtotal = product.Price * quantity;

        await _bot.SendButtons(
            to,
            bodyText: $"✅ Added *{quantity}x {product.Name}* to cart!\n" +
                      $"💰 Subtotal: ₹{addedSubtotal}\n\n" +
                      $"🛒 Cart total: {cartCount} item(s)",
            buttons: new List<ButtonOption>
            {
                new() { Id = "view_cart", Title = "🛒 View Cart" },
                new() { Id = "browse_categories", Title = "🛍️ Continue" },
                new() { Id = "checkout", Title = "💳 Checkout" }
            },
            ct: ct
        );
    }

    public async Task SendCartSummary(string to, int customerId, CancellationToken ct = default)
    {
        var cartItems = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CustomerId == customerId)
            .ToListAsync(ct);

        if (!cartItems.Any())
        {
            // Check if there's a pending unpaid order (cart was converted to order at checkout)
            var pendingOrder = await _db.Orders
                .Where(o => o.CustomerId == customerId
                         && o.Status == OrderStatus.Pending
                         && o.PaymentExpiresAt != null
                         && o.PaymentExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (pendingOrder != null)
            {
                var remaining = pendingOrder.PaymentExpiresAt!.Value - DateTime.UtcNow;
                var mins = (int)remaining.TotalMinutes;
                var secs = remaining.Seconds;
                var baseUrl = ChatBotHelpers.GetPublicBaseUrl(_config);
                var paymentUrl = baseUrl != null
                    ? $"{baseUrl}/api/payment/pay/{Uri.EscapeDataString(pendingOrder.OrderNumber)}"
                    : null;

                await _bot.SendButtons(to,
                    $"⏳ You have a pending order *{pendingOrder.OrderNumber}* (₹{pendingOrder.TotalAmount}).\n\n" +
                    $"Your cart items are in this order — pay within *{mins}m {secs}s* to complete it.\n\n" +
                    (paymentUrl != null ? $"💳 Pay here: {paymentUrl}\n\n" : "") +
                    $"If you no longer want this order, tap *Cancel Order* below — your items will be restored.",
                    new List<ButtonOption>
                    {
                        new() { Id = $"cancel_ord_{pendingOrder.Id}", Title = "❌ Cancel Order" },
                        new() { Id = "main_menu",                     Title = "🏠 Main Menu" }
                    },
                    ct: ct);
                return;
            }

            await _bot.SendButtons(
                to,
                bodyText: "🛒 Your cart is empty!\n\nBrowse our products to add items.",
                buttons: new List<ButtonOption>
                {
                    new() { Id = "browse_categories", Title = "🛍️ Browse" },
                    new() { Id = "main_menu", Title = "🏠 Menu" }
                },
                ct: ct
            );
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("🛒 *Your Cart:*\n");
        decimal total = 0;
        int itemNo = 1;
        foreach (var item in cartItems)
        {
            var subtotal = item.Product.Price * item.Quantity;
            total += subtotal;
            sb.AppendLine($"{itemNo}. {item.Product.Name}");
            sb.AppendLine($"   Qty: {item.Quantity} × ₹{item.Product.Price} = ₹{subtotal}");
            itemNo++;
        }
        sb.AppendLine($"\n💰 *Total: ₹{total}*");

        await _bot.SendButtons(
            to,
            bodyText: sb.ToString(),
            buttons: new List<ButtonOption>
            {
                new() { Id = "checkout", Title = "💳 Checkout" },
                new() { Id = "edit_cart", Title = "✏️ Edit Cart" },
                new() { Id = "browse_categories", Title = "🛍️ Continue" }
            },
            ct: ct
        );
    }

    public async Task ClearCart(string to, int customerId, CancellationToken ct = default)
    {
        var items = await _db.CartItems.Where(ci => ci.CustomerId == customerId).ToListAsync(ct);
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync(ct);

        await _bot.SendText(to, "🗑️ Cart cleared! Type *menu* to browse products.", ct);
    }

    /// <summary>
    /// Shows an interactive list of cart items that the customer can tap to remove,
    /// plus a "Clear All" option. Uses WhatsApp interactive list (supports up to 10 rows).
    /// </summary>
    public async Task SendEditCartList(string to, int customerId, CancellationToken ct = default)
    {
        var cartItems = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CustomerId == customerId)
            .ToListAsync(ct);

        if (!cartItems.Any())
        {
            await _bot.SendButtons(
                to,
                bodyText: "🛒 Your cart is empty!\n\nBrowse our products to add items.",
                buttons: new List<ButtonOption>
                {
                    new() { Id = "browse_categories", Title = "🛍️ Browse" },
                    new() { Id = "main_menu", Title = "🏠 Menu" }
                },
                ct: ct
            );
            return;
        }

        var rows = new List<ListRow>();

        // Add each cart item as a removable row (WhatsApp list supports max 10 rows per section)
        foreach (var item in cartItems.Take(9))
        {
            // Row title max 24 chars, truncate product name if needed
            var name = item.Product.Name.Length > 20
                ? item.Product.Name[..17] + "..."
                : item.Product.Name;

            rows.Add(new ListRow
            {
                Id = $"rmcart_{item.Id}",
                Title = $"❌ {name}",
                Description = $"Qty: {item.Quantity} × ₹{item.Product.Price} = ₹{item.Product.Price * item.Quantity}"
            });
        }

        // Add "Clear All" as the last row (if we have room)
        if (rows.Count < 10)
        {
            rows.Add(new ListRow
            {
                Id = "clear_cart",
                Title = "🗑️ Clear All Items",
                Description = "Remove everything from your cart"
            });
        }

        decimal total = cartItems.Sum(ci => ci.Product.Price * ci.Quantity);
        var itemCount = cartItems.Sum(ci => ci.Quantity);

        await _bot.SendList(
            to,
            headerText: "✏️ Edit Cart",
            bodyText: $"Your cart has *{itemCount} item(s)* (₹{total}).\n\nTap an item below to remove it from your cart:",
            buttonText: "🛒 Select Item",
            sections: new List<ListSection>
            {
                new() { Title = "Cart Items", Rows = rows }
            },
            ct: ct
        );
    }

    /// <summary>
    /// Removes a specific cart item by its database ID.
    /// After removal, re-sends the cart summary so the customer can continue editing or checkout.
    /// </summary>
    public async Task RemoveCartItem(string to, int customerId, int cartItemId, CancellationToken ct = default)
    {
        var cartItem = await _db.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.CustomerId == customerId, ct);

        if (cartItem == null)
        {
            // Item already removed (stale button tap or duplicate webhook)
            await SendCartSummary(to, customerId, ct);
            return;
        }

        var productName = cartItem.Product.Name;
        var qty = cartItem.Quantity;

        _db.CartItems.Remove(cartItem);
        await _db.SaveChangesAsync(ct);

        // Check if cart is now empty
        var remaining = await _db.CartItems.CountAsync(ci => ci.CustomerId == customerId, ct);
        if (remaining == 0)
        {
            await _bot.SendButtons(
                to,
                bodyText: $"✅ Removed *{qty}x {productName}*.\n\n🛒 Your cart is now empty!",
                buttons: new List<ButtonOption>
                {
                    new() { Id = "browse_categories", Title = "🛍️ Browse" },
                    new() { Id = "main_menu", Title = "🏠 Menu" }
                },
                ct: ct
            );
            return;
        }

        // Confirm removal then re-show cart summary so customer can remove more or checkout
        await _bot.SendText(to, $"✅ Removed *{qty}x {productName}* from your cart.", ct);
        await SendCartSummary(to, customerId, ct);
    }
}
