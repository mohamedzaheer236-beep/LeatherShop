using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.Models;

using LeatherShopAPI.Extensions;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Handles the WhatsApp chatbot conversation flow:
///   Main Menu → Browse Categories → View Products → Add to Cart → Checkout → Payment
/// </summary>
public class ChatBotService : IChatBotService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<ChatBotService> _logger;
    private readonly IConfiguration _config;

    public ChatBotService(AppDbContext db, IWhatsAppService whatsApp, ILogger<ChatBotService> logger, IConfiguration config)
    {
        _db = db;
        _whatsApp = whatsApp;
        _logger = logger;
        _config = config;
    }

    /// <summary>Process an incoming WhatsApp message and respond accordingly</summary>
    public async Task ProcessMessage(string from, string name, string messageType, string? textBody, string? interactiveId, string? interactiveTitle)
    {
        // Normalize phone number (WhatsApp sends without '+', ensure consistency)
        var phone = PhoneNumberHelper.Normalize(from);

        // Ensure customer exists
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone);
        if (customer == null)
        {
            customer = new Customer { PhoneNumber = phone, Name = name };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }

        // Determine what the user selected/typed
        var input = (interactiveId ?? textBody ?? "").Trim().ToLower();

        try
        {
            // ---- QUANTITY INPUT (customer is typing how many to add) ----
            if (customer.PendingProductId.HasValue && int.TryParse(input, out var qty))
            {
                await AddToCartWithQuantity(phone, customer, customer.PendingProductId.Value, qty);
                return;
            }

            // If customer had a pending product but typed something else, clear it
            if (customer.PendingProductId.HasValue)
            {
                customer.PendingProductId = null;
                await _db.SaveChangesAsync();
            }

            // ---- MAIN MENU ----
            if (input is "hi" or "hello" or "hey" or "menu" or "start" or "main_menu")
            {
                await SendMainMenu(phone, customer.Name);
                return;
            }

            // ---- BROWSE CATEGORIES ----
            if (input == "browse_categories")
            {
                await SendCategoryList(phone);
                return;
            }

            // ---- SELECTED A CATEGORY ----
            if (input.StartsWith("cat_"))
            {
                var category = input.Replace("cat_", "").Replace("_", " ");
                await SendProductsInCategory(phone, category);
                return;
            }

            // ---- SELECTED A PRODUCT (view details) ----
            if (input.StartsWith("prod_"))
            {
                var productId = int.Parse(input.Replace("prod_", ""));
                await SendProductDetails(phone, productId);
                return;
            }

            // ---- ADD TO CART (ask for quantity) ----
            if (input.StartsWith("addcart_"))
            {
                var productId = int.Parse(input.Replace("addcart_", ""));
                await AskQuantity(phone, customer, productId);
                return;
            }

            // ---- VIEW CART ----
            if (input == "view_cart")
            {
                await SendCartSummary(phone, customer.Id);
                return;
            }

            // ---- CLEAR CART ----
            if (input == "clear_cart")
            {
                await ClearCart(phone, customer.Id);
                return;
            }

            // ---- CHECKOUT ----
            if (input == "checkout")
            {
                await ProcessCheckout(phone, customer);
                return;
            }

            // ---- MY ORDERS ----
            if (input == "my_orders")
            {
                await SendOrderHistory(phone, customer.Id);
                return;
            }

            // ---- DEFAULT: show main menu ----
            await _whatsApp.SendTextMessage(phone, "🙏 Welcome to our Leather Shop! Type *menu* to see options.");
            await SendMainMenu(phone, customer.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {Phone}", phone);
            await _whatsApp.SendTextMessage(phone, "Sorry, something went wrong. Please type *menu* to start again.");
        }
    }

    // ================================================
    //  MENU FLOWS
    // ================================================

    private async Task SendMainMenu(string to, string customerName)
    {
        var greeting = string.IsNullOrEmpty(customerName) ? "Welcome!" : $"Hello {customerName}! 👋";

        await _whatsApp.SendListMessage(
            to,
            headerText: "🛍️ Leather Shop",
            bodyText: $"{greeting}\n\nWe offer premium handcrafted leather products.\n\nWhat would you like to do?",
            buttonText: "📋 View Menu",
            sections: new List<ListSection>
            {
                new()
                {
                    Title = "Shop",
                    Rows = new List<ListRow>
                    {
                        new() { Id = "browse_categories", Title = "🏷️ Browse Categories", Description = "Wallets, Belts, Bags, Shoes & more" },
                        new() { Id = "view_cart", Title = "🛒 View Cart", Description = "See items in your cart" },
                        new() { Id = "checkout", Title = "💳 Checkout", Description = "Place your order & pay" }
                    }
                },
                new()
                {
                    Title = "Account",
                    Rows = new List<ListRow>
                    {
                        new() { Id = "my_orders", Title = "📦 My Orders", Description = "Track your order status" }
                    }
                }
            }
        );
    }

    private async Task SendCategoryList(string to)
    {
        var categories = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity > 0)
            .Select(p => p.Category)
            .Distinct()
            .ToListAsync();

        if (!categories.Any())
        {
            await _whatsApp.SendTextMessage(to, "Sorry, no products available right now. Please check back later!");
            return;
        }

        var rows = categories.Select(cat => new ListRow
        {
            Id = $"cat_{cat.ToLower().Replace(" ", "_")}",
            Title = cat,
            Description = $"Browse {cat} collection"
        }).ToList();

        await _whatsApp.SendListMessage(
            to,
            headerText: "📂 Categories",
            bodyText: "Select a category to browse products:",
            buttonText: "🏷️ Categories",
            sections: new List<ListSection>
            {
                new() { Title = "Available Categories", Rows = rows }
            }
        );
    }

    private async Task SendProductsInCategory(string to, string category)
    {
        var products = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity > 0 && p.Category.ToLower() == category)
            .Take(10) // WhatsApp list max 10 rows per section
            .ToListAsync();

        if (!products.Any())
        {
            await _whatsApp.SendTextMessage(to, $"No products found in '{category}'. Type *menu* to browse other categories.");
            return;
        }

        var rows = products.Select(p => new ListRow
        {
            Id = $"prod_{p.Id}",
            Title = p.Name.Length > 24 ? p.Name[..24] : p.Name,
            Description = $"₹{p.Price} | {p.Brand} | Stock: {p.StockQuantity}"
        }).ToList();

        await _whatsApp.SendListMessage(
            to,
            headerText: $"🛍️ {char.ToUpper(category[0]) + category[1..]}",
            bodyText: $"Here are our {category} products. Tap to view details:",
            buttonText: "📦 View Products",
            sections: new List<ListSection>
            {
                new() { Title = $"{category} Products", Rows = rows }
            }
        );
    }

    private async Task SendProductDetails(string to, int productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null)
        {
            await _whatsApp.SendTextMessage(to, "Product not found. Type *menu* to browse.");
            return;
        }

        var details = $"*{product.Name}*\n\n" +
                      $"🏷️ Brand: {product.Brand}\n" +
                      $"📂 Category: {product.Category}\n" +
                      $"💰 Price: ₹{product.Price}\n" +
                      $"📦 In Stock: {product.StockQuantity}\n\n" +
                      $"📝 {product.Description}";

        // Send product image if available
        if (!string.IsNullOrEmpty(product.ImageUrl))
        {
            var baseUrl = _config["App:BaseUrl"]?.TrimEnd('/');
            // Fallback: use Railway's auto-provided public domain
            if (string.IsNullOrEmpty(baseUrl) || baseUrl.Contains("WILL_BE_SET"))
            {
                var railwayDomain = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN");
                if (!string.IsNullOrEmpty(railwayDomain))
                    baseUrl = $"https://{railwayDomain}";
            }

            var imageFullUrl = product.ImageUrl.StartsWith("http")
                ? product.ImageUrl
                : $"{baseUrl}{product.ImageUrl}";

            _logger.LogWarning("Sending product image: {FullUrl}", imageFullUrl);

            try
            {
                // Caption max 1024 chars for WhatsApp image messages
                var caption = details.Length > 1024 ? details[..1021] + "..." : details;
                await _whatsApp.SendImageMessage(to, imageFullUrl, caption);

                // Send action buttons separately (image messages don't support inline buttons)
                await _whatsApp.SendButtonMessage(
                    to,
                    bodyText: "What would you like to do?",
                    buttons: new List<ButtonOption>
                    {
                        new() { Id = $"addcart_{product.Id}", Title = "🛒 Add to Cart" },
                        new() { Id = "browse_categories", Title = "🔙 Categories" },
                        new() { Id = "main_menu", Title = "🏠 Main Menu" }
                    }
                );
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send product image for product {ProductId}, falling back to text", productId);
                // Fall through to text-only message below
            }
        }

        // Send product details with action buttons (text-only fallback)
        var bodyText = details.Length > 1024 ? details[..1021] + "..." : details;
        await _whatsApp.SendButtonMessage(
            to,
            bodyText: bodyText,
            buttons: new List<ButtonOption>
            {
                new() { Id = $"addcart_{product.Id}", Title = "🛒 Add to Cart" },
                new() { Id = "browse_categories", Title = "🔙 Categories" },
                new() { Id = "main_menu", Title = "🏠 Main Menu" }
            }
        );
    }

    /// <summary>Ask the customer how many they want to add</summary>
    private async Task AskQuantity(string to, Customer customer, int productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null || !product.IsActive || product.StockQuantity <= 0)
        {
            await _whatsApp.SendTextMessage(to, "Sorry, this product is no longer available.");
            return;
        }

        // Save pending product so we know what they're adding when they type a number
        customer.PendingProductId = productId;
        await _db.SaveChangesAsync();

        await _whatsApp.SendTextMessage(to,
            $"How many *{product.Name}* would you like to add?\n\n" +
            $"📦 Available: *{product.StockQuantity}*\n" +
            $"💰 Price: ₹{product.Price} each\n\n" +
            $"Type a number (e.g. *1*, *2*, *5*):");
    }

    /// <summary>Add to cart with the quantity the customer typed</summary>
    private async Task AddToCartWithQuantity(string to, Customer customer, int productId, int quantity)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null || !product.IsActive || product.StockQuantity <= 0)
        {
            customer.PendingProductId = null;
            await _db.SaveChangesAsync();
            await _whatsApp.SendTextMessage(to, "Sorry, this product is no longer available.");
            return;
        }

        // Validate quantity
        if (quantity <= 0)
        {
            await _whatsApp.SendTextMessage(to, "❌ Please enter a valid number greater than 0.");
            return; // Keep PendingProductId so they can try again
        }

        // Check against existing cart quantity + new quantity vs stock
        var existingItem = await _db.CartItems
            .FirstOrDefaultAsync(ci => ci.CustomerId == customer.Id && ci.ProductId == productId);
        var alreadyInCart = existingItem?.Quantity ?? 0;
        var totalNeeded = alreadyInCart + quantity;

        if (totalNeeded > product.StockQuantity)
        {
            var canAdd = product.StockQuantity - alreadyInCart;
            if (canAdd <= 0)
            {
                customer.PendingProductId = null;
                await _db.SaveChangesAsync();
                await _whatsApp.SendButtonMessage(to,
                    bodyText: $"❌ You already have *{alreadyInCart}* of *{product.Name}* in your cart, which is the maximum available stock.\n\nYou can't add more.",
                    buttons: new List<ButtonOption>
                    {
                        new() { Id = "view_cart", Title = "🛒 View Cart" },
                        new() { Id = "browse_categories", Title = "🛍️ Browse" },
                        new() { Id = "checkout", Title = "💳 Checkout" }
                    });
                return;
            }

            await _whatsApp.SendTextMessage(to,
                $"❌ Sorry, we only have *{product.StockQuantity}* of *{product.Name}* in stock." +
                (alreadyInCart > 0 ? $"\nYou already have *{alreadyInCart}* in your cart, so you can add up to *{canAdd}* more." : "") +
                $"\n\nPlease type a number between *1* and *{canAdd}*:");
            return; // Keep PendingProductId so they can try again
        }

        // Add to cart
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
                Quantity = quantity
            });
        }

        // Clear pending state
        customer.PendingProductId = null;
        await _db.SaveChangesAsync();

        var cartCount = await _db.CartItems.Where(ci => ci.CustomerId == customer.Id).SumAsync(ci => ci.Quantity);
        var addedSubtotal = product.Price * quantity;

        await _whatsApp.SendButtonMessage(
            to,
            bodyText: $"✅ Added *{quantity}x {product.Name}* to cart!\n" +
                      $"💰 Subtotal: ₹{addedSubtotal}\n\n" +
                      $"🛒 Cart total: {cartCount} item(s)",
            buttons: new List<ButtonOption>
            {
                new() { Id = "view_cart", Title = "🛒 View Cart" },
                new() { Id = "browse_categories", Title = "🛍️ Continue" },
                new() { Id = "checkout", Title = "💳 Checkout" }
            }
        );
    }
    private async Task SendCartSummary(string to, int customerId)
    {
        var cartItems = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CustomerId == customerId)
            .ToListAsync();

        if (!cartItems.Any())
        {
            await _whatsApp.SendButtonMessage(
                to,
                bodyText: "🛒 Your cart is empty!\n\nBrowse our products to add items.",
                buttons: new List<ButtonOption>
                {
                    new() { Id = "browse_categories", Title = "🛍️ Browse" },
                    new() { Id = "main_menu", Title = "🏠 Menu" }
                }
            );
            return;
        }

        var sb = new System.Text.StringBuilder();
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

        await _whatsApp.SendButtonMessage(
            to,
            bodyText: sb.ToString(),
            buttons: new List<ButtonOption>
            {
                new() { Id = "checkout", Title = "💳 Checkout" },
                new() { Id = "clear_cart", Title = "🗑️ Clear Cart" },
                new() { Id = "browse_categories", Title = "🛍️ Continue" }
            }
        );
    }

    private async Task ClearCart(string to, int customerId)
    {
        var items = await _db.CartItems.Where(ci => ci.CustomerId == customerId).ToListAsync();
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync();

        await _whatsApp.SendTextMessage(to, "🗑️ Cart cleared! Type *menu* to browse products.");
    }

    private async Task ProcessCheckout(string to, Customer customer)
    {
        var cartItems = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CustomerId == customer.Id)
            .ToListAsync();

        if (!cartItems.Any())
        {
            await _whatsApp.SendTextMessage(to, "🛒 Your cart is empty! Browse products first.");
            return;
        }

        // Check stock availability
        foreach (var item in cartItems)
        {
            if (item.Product.StockQuantity < item.Quantity)
            {
                await _whatsApp.SendTextMessage(to, $"❌ Sorry, *{item.Product.Name}* only has {item.Product.StockQuantity} left in stock. Please update your cart.");
                return;
            }
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
            OrderItems = cartItems.Select(ci => new OrderItem
            {
                ProductId = ci.ProductId,
                Quantity = ci.Quantity,
                UnitPrice = ci.Product.Price
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
        await _db.SaveChangesAsync();

        // Generate payment link (replace with actual Razorpay integration)
        var paymentUrl = $"{_config["App:BaseUrl"]}/api/payment/pay/{order.Id}";

        var orderSummary = $"✅ *Order Placed!*\n\n" +
                           $"📋 Order: *{order.OrderNumber}*\n" +
                           $"💰 Total: *₹{total}*\n\n" +
                           $"💳 Pay here: {paymentUrl}\n\n" +
                           $"We'll confirm once payment is received.";

        await _whatsApp.SendTextMessage(to, orderSummary);
    }

    private async Task SendOrderHistory(string to, int customerId)
    {
        var orders = await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync();

        if (!orders.Any())
        {
            await _whatsApp.SendTextMessage(to, "📦 You don't have any orders yet. Start shopping!");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("📦 *Your Recent Orders:*\n");
        foreach (var order in orders)
        {
            sb.AppendLine($"🔸 *{order.OrderNumber}*");
            sb.AppendLine($"   Amount: ₹{order.TotalAmount}");
            sb.AppendLine($"   Status: {order.Status}");
            sb.AppendLine($"   Paid: {(order.IsPaid ? "✅ Yes" : "❌ No")}");
            sb.AppendLine($"   Date: {order.CreatedAt:dd-MMM-yyyy}");
            sb.AppendLine();
        }

        await _whatsApp.SendTextMessage(to, sb.ToString());
    }
}
