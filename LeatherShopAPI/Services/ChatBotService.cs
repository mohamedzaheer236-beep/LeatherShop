using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Models;

using System.Text;
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
    private readonly IChatService _chatService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<ChatBotService> _logger;
    private readonly IConfiguration _config;

    // Set per-request to save outgoing bot messages (scoped service = safe)
    private int? _currentCustomerId;

    public ChatBotService(AppDbContext db, IWhatsAppService whatsApp, IChatService chatService,
        IHubContext<NotificationHub> hubContext, ILogger<ChatBotService> logger, IConfiguration config)
    {
        _db = db;
        _whatsApp = whatsApp;
        _chatService = chatService;
        _hubContext = hubContext;
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Returns the public base URL for constructing externally-reachable links (images, payment).
    /// Prefers App:BaseUrl config, falls back to RAILWAY_PUBLIC_DOMAIN env var.
    /// Skips localhost/placeholder values since WhatsApp servers can't reach them.
    /// </summary>
    private string? GetPublicBaseUrl()
    {
        var baseUrl = _config["App:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl) || baseUrl.Contains("WILL_BE_SET") || baseUrl.Contains("localhost"))
        {
            var railwayDomain = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN");
            if (!string.IsNullOrEmpty(railwayDomain))
                baseUrl = $"https://{railwayDomain}";
            else
                baseUrl = null;
        }
        return baseUrl;
    }

    // ================================================
    //  SEND + SAVE HELPERS (wraps WhatsApp send + saves to chat history + pushes via SignalR)
    // ================================================

    private async Task BotSendText(string to, string message)
    {
        await _whatsApp.SendTextMessage(to, message);
        await SaveAndPushBotMessage(message, "text");
    }

    private async Task BotSendList(string to, string headerText, string bodyText, string buttonText, List<ListSection> sections)
    {
        await _whatsApp.SendListMessage(to, headerText, bodyText, buttonText, sections);
        await SaveAndPushBotMessage($"{headerText}\n{bodyText}", "interactive");
    }

    private async Task BotSendButtons(string to, string bodyText, List<ButtonOption> buttons)
    {
        await _whatsApp.SendButtonMessage(to, bodyText, buttons);
        await SaveAndPushBotMessage(bodyText, "interactive");
    }

    private async Task BotSendImage(string to, string imageUrl, string? caption)
    {
        await _whatsApp.SendImageMessage(to, imageUrl, caption);
        await SaveAndPushBotMessage(caption ?? "[image]", "image");
    }

    private async Task BotSendCarousel(string to, string templateName, string bodyText, List<CarouselCard> cards)
    {
        await _whatsApp.SendCarouselTemplateMessage(to, templateName, cards);
        await SaveAndPushBotMessage($"[carousel] {bodyText}", "template");
    }

    private async Task SaveAndPushBotMessage(string content, string messageType)
    {
        if (!_currentCustomerId.HasValue) return;
        try
        {
            var saved = await _chatService.SaveMessageAsync(
                _currentCustomerId.Value, MessageDirection.Outgoing, content, "Bot", true, messageType);

            await _hubContext.Clients.Group($"chat_{_currentCustomerId.Value}").SendAsync("ReceiveMessage", new ChatMessageDto
            {
                Id = saved.Id,
                CustomerId = _currentCustomerId.Value,
                Direction = "Outgoing",
                MessageType = saved.MessageType,
                Content = saved.Content,
                SenderName = "Bot",
                IsFromBot = true,
                Timestamp = saved.Timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save/push bot message for customer {CustomerId}", _currentCustomerId);
        }
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

        // Track current customer for bot message saving
        _currentCustomerId = customer.Id;

        // Determine what the user selected/typed
        var input = (interactiveId ?? textBody ?? "").Trim().ToLower();

        try
        {
            // ---- ADDRESS CONFIRMATION (customer reviewing saved address before checkout) ----
            if (customer.PendingAction == Customer.PendingActions.ConfirmingAddress)
            {
                if (interactiveId == "confirm_address")
                {
                    customer.PendingAction = null;
                    await _db.SaveChangesAsync();
                    await PlaceOrder(phone, customer);
                    return;
                }
                if (interactiveId == "change_address")
                {
                    customer.PendingAction = Customer.PendingActions.AwaitingAddress;
                    await _db.SaveChangesAsync();
                    await BotSendText(phone,
                        "📍 *Enter your new shipping address:*\n\n" +
                        "_Example: 123, MG Road, Anna Nagar, Chennai - 600040_");
                    return;
                }
                // If user tapped some other button, cancel and fall through
                if (!string.IsNullOrEmpty(interactiveId))
                {
                    customer.PendingAction = null;
                    await _db.SaveChangesAsync();
                }
                else
                {
                    await BotSendText(phone, "Please tap *✅ Confirm* or *✏️ Change Address* above.");
                    return;
                }
            }

            // ---- ADDRESS INPUT (customer is providing shipping address for checkout) ----
            if (customer.PendingAction == Customer.PendingActions.AwaitingAddress)
            {
                // If user tapped an interactive button, cancel the address prompt and process normally
                if (!string.IsNullOrEmpty(interactiveId))
                {
                    customer.PendingAction = null;
                    await _db.SaveChangesAsync();
                    // Fall through to normal flow below
                }
                else
                {
                    var rawAddress = (textBody ?? "").Trim();
                    if (rawAddress.Length >= 10)
                    {
                        await HandleAddressInput(phone, customer, rawAddress);
                        return;
                    }
                    // Too short — ask again
                    await BotSendText(phone, "📍 That seems too short. Please enter your *full shipping address* (at least 10 characters):\n\nExample: _123, MG Road, Anna Nagar, Chennai - 600040_");
                    return;
                }
            }

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
                customer.PendingImageId = null;
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
                var category = input["cat_".Length..].Replace("_", " ");
                if (string.IsNullOrWhiteSpace(category))
                {
                    await BotSendText(phone, "Invalid category. Type *menu* to browse our options.");
                    return;
                }
                await SendProductsInCategory(phone, category);
                return;
            }

            // ---- SELECTED A PRODUCT (view details) ----
            if (input.StartsWith("prod_"))
            {
                if (int.TryParse(input.Replace("prod_", ""), out var productId))
                {
                    await SendProductDetails(phone, productId);
                    return;
                }
                await BotSendText(phone, "Invalid product. Type *menu* to browse.");
                return;
            }

            // ---- VIEW PRODUCT FROM CAROUSEL (quick_reply "View Details" button) ----
            // Payload format: view_{productId}_pi{imageId} or legacy view_{productId}
            if (input.StartsWith("view_") && input != "view_cart")
            {
                var (viewProdId, viewImgId) = ParseProductImagePayload(input, "view_");
                if (viewProdId.HasValue)
                {
                    await SendProductDetailsText(phone, viewProdId.Value, viewImgId);
                    return;
                }
                await BotSendText(phone, "Invalid product. Type *menu* to browse.");
                return;
            }

            // ---- ADD TO CART (ask for quantity) ----
            // Payload format: addcart_{productId}_pi{imageId} or legacy addcart_{productId}
            if (input.StartsWith("addcart_"))
            {
                var (cartProdId, cartImgId) = ParseProductImagePayload(input, "addcart_");
                if (cartProdId.HasValue)
                {
                    await AskQuantity(phone, customer, cartProdId.Value, cartImgId);
                    return;
                }
                await BotSendText(phone, "Invalid product. Type *menu* to browse.");
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
            await BotSendText(phone, "🙏 Welcome to our Leather Shop! Type *menu* to see options.");
            await SendMainMenu(phone, customer.Name);
        }
        catch (WhatsAppApiException ex)
        {
            // WhatsApp API failure (rate limit, etc.) — do NOT try to send another message
            // as that would also likely fail and worsen the rate limit situation.
            _logger.LogError(ex, "WhatsApp API error processing message from {Phone}", phone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {Phone}", phone);
            try
            {
                await BotSendText(phone, "Sorry, something went wrong. Please type *menu* to start again.");
            }
            catch (Exception sendEx)
            {
                _logger.LogWarning(sendEx, "Failed to send error message to {Phone}", phone);
            }
        }
    }

    // ================================================
    //  MENU FLOWS
    // ================================================

    private async Task SendMainMenu(string to, string customerName)
    {
        var greeting = string.IsNullOrEmpty(customerName) ? "Welcome!" : $"Hello {customerName}! 👋";

        await BotSendList(
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
            .OrderBy(c => c)
            .ToListAsync();

        if (!categories.Any())
        {
            await BotSendText(to, "Sorry, no products available right now. Please check back later!");
            return;
        }

        var rows = categories.Select(cat => new ListRow
        {
            Id = $"cat_{cat.ToLower().Replace(" ", "_")}",
            Title = cat,
            Description = $"Browse {cat} collection"
        }).ToList();

        await BotSendList(
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
            .Where(p => p.IsActive && p.StockQuantity > 0 && EF.Functions.ILike(p.Category, category))
            .OrderBy(p => p.Name)
            .Take(10) // WhatsApp list max 10 rows per section
            .ToListAsync();

        if (!products.Any())
        {
            await BotSendText(to, $"No products found in '{category}'. Type *menu* to browse other categories.");
            return;
        }

        var rows = products.Select(p => new ListRow
        {
            Id = $"prod_{p.Id}",
            Title = p.Name.Length > 24 ? p.Name[..24] : p.Name,
            Description = $"₹{p.Price} | {p.Brand} | Stock: {p.StockQuantity}"
        }).ToList();

        await BotSendList(
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
        var product = await _db.Products.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null)
        {
            await BotSendText(to, "Product not found. Type *menu* to browse.");
            return;
        }

        var details = $"*{product.Name}*\n\n" +
                      $"🏷️ Brand: {product.Brand}\n" +
                      $"📂 Category: {product.Category}\n" +
                      $"💰 Price: ₹{product.Price}\n" +
                      $"📦 In Stock: {product.StockQuantity}\n\n" +
                      $"📝 {product.Description}";

        // Build the full list of image URLs (primary first, then additional ordered)
        // Also build a parallel list of image IDs: 0 = primary, otherwise ProductImage.Id
        var imageUrls = new List<string>();
        var imageIds = new List<int>();  // parallel to imageUrls: 0 = primary, N = ProductImage.Id
        if (!string.IsNullOrEmpty(product.ImageUrl))
        {
            imageUrls.Add(product.ImageUrl);
            imageIds.Add(0);  // 0 means primary image (not in ProductImages table)
        }
        if (product.Images.Count > 0)
        {
            foreach (var img in product.Images.OrderBy(i => i.DisplayOrder))
            {
                imageUrls.Add(img.ImageUrl);
                imageIds.Add(img.Id);
            }
        }

        // Send product images if available
        if (imageUrls.Count > 0)
        {
            var baseUrl = GetPublicBaseUrl();
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogWarning("GetPublicBaseUrl() returned null — skipping image sends for product {ProductId}", productId);
                // Fall through to text-only fallback below
            }
            else try
            {
                // WhatsApp template headers only support JPEG & PNG — filter for carousel
                var carouselSupportedExts = new[] { ".jpg", ".jpeg", ".png" };
                var carouselImageUrls = imageUrls
                    .Where(u => carouselSupportedExts.Contains(Path.GetExtension(u).ToLower()))
                    .ToList();

                // If we have multiple carousel-compatible images, try to send as carousel template
                // Templates: product_gallery (2 cards), product_gallery_3 (3 cards), product_gallery_4 (4 cards)
                if (carouselImageUrls.Count >= 2)
                {
                    try
                    {
                        // Pick the right template based on available images (max 4 cards)
                        var cardCount = Math.Min(carouselImageUrls.Count, 4);
                        var templateName = cardCount switch
                        {
                            2 => "product_gallery",
                            3 => "product_gallery_3",
                            _ => "product_gallery_4"  // 4 or more images → 4-card template
                        };

                        // Build a filtered list of image IDs that match carousel-supported images
                        var carouselImageIds = imageUrls
                            .Select((url, idx) => new { url, id = imageIds[idx] })
                            .Where(x => carouselSupportedExts.Contains(Path.GetExtension(x.url).ToLower()))
                            .Select(x => x.id)
                            .ToList();

                        var cards = new List<CarouselCard>();
                        for (int i = 0; i < cardCount; i++)
                        {
                            var url = carouselImageUrls[i];
                            var imgId = carouselImageIds[i];
                            var imageFullUrl = url.StartsWith("http") ? url : $"{baseUrl}{url}";
                            cards.Add(new CarouselCard
                            {
                                ImageUrl = imageFullUrl,
                                BodyParam = $"{product.Name} - ₹{product.Price}",
                                ButtonPayload = $"view_{product.Id}_pi{imgId}"
                            });
                        }

                        // Send carousel only — user taps "View Details" to see full product info
                        await BotSendCarousel(to, templateName, $"Browse {product.Name} images", cards);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Carousel template failed for product {ProductId}, falling back to individual images", productId);
                        // Fall through to individual image sending below
                    }
                }

                // Single image or carousel fallback: send images individually
                var caption = details.Length > 1024 ? details[..1021] + "..." : details;

                for (int i = 0; i < imageUrls.Count; i++)
                {
                    var url = imageUrls[i];
                    var imageFullUrl = url.StartsWith("http") ? url : $"{baseUrl}{url}";
                    var imgCaption = (i == 0) ? caption : null;

                    _logger.LogInformation("Sending product image {Index}/{Total}: {FullUrl}",
                        i + 1, imageUrls.Count, imageFullUrl);

                    await BotSendImage(to, imageFullUrl, imgCaption);
                }

                // Send action buttons separately (image messages don't support inline buttons)
                // Best-effort: if buttons fail (e.g. rate limit), images were already sent.
                try
                {
                    await BotSendButtons(
                        to,
                        bodyText: "What would you like to do?",
                        buttons: new List<ButtonOption>
                        {
                            new() { Id = $"addcart_{product.Id}", Title = "🛒 Add to Cart" },
                            new() { Id = "browse_categories", Title = "🔙 Categories" },
                            new() { Id = "main_menu", Title = "🏠 Main Menu" }
                        }
                    );
                }
                catch (WhatsAppApiException ex)
                {
                    _logger.LogWarning(ex, "Failed to send action buttons after product images (rate limit), product {ProductId}", productId);
                }
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send product images for product {ProductId}, falling back to text", productId);
                // Fall through to text-only fallback below
            }
        }

        // Text fallback: send product details with action buttons
        var bodyText = details.Length > 1024 ? details[..1021] + "..." : details;
        try
        {
            await BotSendButtons(
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
        catch (WhatsAppApiException ex)
        {
            // If images were already sent above, buttons are non-critical.
            // If no images (direct TextFallback), we still can't do anything during rate limit.
            _logger.LogWarning(ex, "Failed to send product detail buttons for product {ProductId}", productId);
        }
    }

    /// <summary>
    /// Send product details as text + action buttons only (no images/carousel).
    /// Used when user taps "View Details" from the carousel — they've already seen the images.
    /// </summary>
    private async Task SendProductDetailsText(string to, int productId, int? selectedImageId = null)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null)
        {
            await BotSendText(to, "Product not found. Type *menu* to browse.");
            return;
        }

        var details = $"*{product.Name}*\n\n" +
                      $"🏷️ Brand: {product.Brand}\n" +
                      $"📂 Category: {product.Category}\n" +
                      $"💰 Price: ₹{product.Price}\n" +
                      $"📦 In Stock: {product.StockQuantity}\n\n" +
                      $"📝 {product.Description}";

        // Carry the selected image ID forward into the addcart_ payload
        var addCartPayload = selectedImageId.HasValue
            ? $"addcart_{product.Id}_pi{selectedImageId.Value}"
            : $"addcart_{product.Id}";

        var bodyText = details.Length > 1024 ? details[..1021] + "..." : details;
        await BotSendButtons(
            to,
            bodyText: bodyText,
            buttons: new List<ButtonOption>
            {
                new() { Id = addCartPayload, Title = "🛒 Add to Cart" },
                new() { Id = "browse_categories", Title = "🔙 Categories" },
                new() { Id = "main_menu", Title = "🏠 Main Menu" }
            }
        );
    }

    /// <summary>Ask the customer how many they want to add</summary>
    private async Task AskQuantity(string to, Customer customer, int productId, int? selectedImageId = null)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null || !product.IsActive || product.StockQuantity <= 0)
        {
            await BotSendText(to, "Sorry, this product is no longer available.");
            return;
        }

        // Save pending product and selected image so we know what to add when they type a number
        customer.PendingProductId = productId;
        customer.PendingImageId = selectedImageId;
        await _db.SaveChangesAsync();

        await BotSendText(to,
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
            customer.PendingImageId = null;
            await _db.SaveChangesAsync();
            await BotSendText(to, "Sorry, this product is no longer available.");
            return;
        }

        // Validate quantity
        if (quantity <= 0)
        {
            await BotSendText(to, "❌ Please enter a valid number greater than 0.");
            return; // Keep PendingProductId so they can try again
        }

        // Check against existing cart quantity + new quantity vs stock
        // Stock check: sum ALL items for this product across all image selections
        var alreadyInCart = await _db.CartItems
            .Where(ci => ci.CustomerId == customer.Id && ci.ProductId == productId)
            .SumAsync(ci => ci.Quantity);
        var totalNeeded = alreadyInCart + quantity;

        if (totalNeeded > product.StockQuantity)
        {
            var canAdd = product.StockQuantity - alreadyInCart;
            if (canAdd <= 0)
            {
                customer.PendingProductId = null;
                customer.PendingImageId = null;
                await _db.SaveChangesAsync();
                await BotSendButtons(to,
                    bodyText: $"❌ You already have *{alreadyInCart}* of *{product.Name}* in your cart, which is the maximum available stock.\n\nYou can't add more.",
                    buttons: new List<ButtonOption>
                    {
                        new() { Id = "view_cart", Title = "🛒 View Cart" },
                        new() { Id = "browse_categories", Title = "🛍️ Browse" },
                        new() { Id = "checkout", Title = "💳 Checkout" }
                    });
                return;
            }

            await BotSendText(to,
                $"❌ Sorry, we only have *{product.StockQuantity}* of *{product.Name}* in stock." +
                (alreadyInCart > 0 ? $"\nYou already have *{alreadyInCart}* in your cart, so you can add up to *{canAdd}* more." : "") +
                $"\n\nPlease type a number between *1* and *{canAdd}*:");
            return; // Keep PendingProductId so they can try again
        }

        // Add to cart — merge only when same product AND same selected image
        var pendingImageId = customer.PendingImageId;
        var existingItem = await _db.CartItems
            .FirstOrDefaultAsync(ci => ci.CustomerId == customer.Id
                                    && ci.ProductId == productId
                                    && ci.SelectedImageId == pendingImageId);

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
                SelectedImageId = customer.PendingImageId
            });
        }

        // Clear pending state
        customer.PendingProductId = null;
        customer.PendingImageId = null;
        await _db.SaveChangesAsync();

        var cartCount = await _db.CartItems.Where(ci => ci.CustomerId == customer.Id).SumAsync(ci => ci.Quantity);
        var addedSubtotal = product.Price * quantity;

        await BotSendButtons(
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
            // Check if there's a pending unpaid order (cart was converted to order at checkout)
            var pendingOrder = await _db.Orders
                .Where(o => o.CustomerId == customerId
                         && o.Status == OrderStatus.Pending
                         && o.PaymentExpiresAt != null
                         && o.PaymentExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (pendingOrder != null)
            {
                var remaining = pendingOrder.PaymentExpiresAt!.Value - DateTime.UtcNow;
                var mins = (int)remaining.TotalMinutes;
                var secs = remaining.Seconds;
                var baseUrl = GetPublicBaseUrl();
                var paymentUrl = baseUrl != null
                    ? $"{baseUrl}/api/payment/pay/{Uri.EscapeDataString(pendingOrder.OrderNumber)}"
                    : null;

                var msg = $"⏳ You have a pending order *{pendingOrder.OrderNumber}* (₹{pendingOrder.TotalAmount}).\n\n" +
                          $"Your cart items are in this order — pay within *{mins}m {secs}s* to complete it.\n\n" +
                          (paymentUrl != null ? $"💳 Pay here: {paymentUrl}\n\n" : "") +
                          $"If you don't pay in time, your items will be restored to the cart automatically.";

                await BotSendText(to, msg);
                return;
            }

            await BotSendButtons(
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

        await BotSendButtons(
            to,
            bodyText: sb.ToString(),
            buttons: new List<ButtonOption>
            {
                new() { Id = "checkout", Title = "💳 Checkout" },
                new() { Id = "clear_cart", Title = "🗑️ Clear Cart" },
                new() { Id = "browse_categories", Title = "🛒️ Continue" }
            }
        );
    }

    private async Task ClearCart(string to, int customerId)
    {
        var items = await _db.CartItems.Where(ci => ci.CustomerId == customerId).ToListAsync();
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync();

        await BotSendText(to, "🗑️ Cart cleared! Type *menu* to browse products.");
    }

    private async Task ProcessCheckout(string to, Customer customer)
    {
        var cartItems = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CustomerId == customer.Id)
            .ToListAsync();

        if (!cartItems.Any())
        {
            // Check if there's a pending unpaid order
            var pendingOrder = await _db.Orders
                .Where(o => o.CustomerId == customer.Id
                         && o.Status == OrderStatus.Pending
                         && o.PaymentExpiresAt != null
                         && o.PaymentExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (pendingOrder != null)
            {
                var baseUrl = GetPublicBaseUrl();
                var paymentUrl = baseUrl != null
                    ? $"{baseUrl}/api/payment/pay/{Uri.EscapeDataString(pendingOrder.OrderNumber)}"
                    : null;

                await BotSendText(to,
                    $"⏳ You already have a pending order *{pendingOrder.OrderNumber}* (₹{pendingOrder.TotalAmount}).\n\n" +
                    (paymentUrl != null ? $"💳 Pay here: {paymentUrl}\n\n" : "") +
                    $"Complete the payment first, or wait for it to expire to get a new checkout link.");
                return;
            }

            await BotSendText(to, "🛒 Your cart is empty! Browse products first.");
            return;
        }

        // Check stock availability
        foreach (var item in cartItems)
        {
            if (item.Product.StockQuantity < item.Quantity)
            {
                await BotSendText(to, $"❌ Sorry, *{item.Product.Name}* only has {item.Product.StockQuantity} left in stock. Please update your cart.");
                return;
            }
        }

        // Ask for shipping address if not set
        if (string.IsNullOrWhiteSpace(customer.Address))
        {
            customer.PendingAction = Customer.PendingActions.AwaitingAddress;
            await _db.SaveChangesAsync();

            await BotSendText(to,
                "📍 *Shipping Address Required*\n\n" +
                "Please type your full shipping address:\n\n" +
                "_Example: 123, MG Road, Anna Nagar, Chennai - 600040_");
            return;
        }

        // Address exists — ask the customer to confirm or change it
        customer.PendingAction = Customer.PendingActions.ConfirmingAddress;
        await _db.SaveChangesAsync();

        var cartTotal = cartItems.Sum(ci => ci.Product.Price * ci.Quantity);
        var itemLines = string.Join("\n", cartItems.Select(ci =>
            $"  • {ci.Product.Name} x{ci.Quantity} — ₹{ci.Product.Price * ci.Quantity}"));

        await BotSendButtons(to,
            $"📋 *Order Summary*\n\n" +
            $"{itemLines}\n" +
            $"💰 Total: *₹{cartTotal}*\n\n" +
            $"📍 *Shipping to:*\n_{customer.Address}_\n\n" +
            $"Is this address correct?",
            new List<ButtonOption>
            {
                new() { Id = "confirm_address", Title = "✅ Confirm" },
                new() { Id = "change_address", Title = "✏️ Change Address" }
            });
    }

    /// <summary>Actually creates the order after address is confirmed</summary>
    private async Task PlaceOrder(string to, Customer customer)
    {
        var cartItems = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CustomerId == customer.Id)
            .ToListAsync();

        if (!cartItems.Any())
        {
            // Check if there's a pending unpaid order
            var pendingOrder = await _db.Orders
                .Where(o => o.CustomerId == customer.Id
                         && o.Status == OrderStatus.Pending
                         && o.PaymentExpiresAt != null
                         && o.PaymentExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (pendingOrder != null)
            {
                var pendingBaseUrl = GetPublicBaseUrl();
                var pendingPayUrl = pendingBaseUrl != null
                    ? $"{pendingBaseUrl}/api/payment/pay/{Uri.EscapeDataString(pendingOrder.OrderNumber)}"
                    : null;

                await BotSendText(to,
                    $"⏳ You already have a pending order *{pendingOrder.OrderNumber}* (₹{pendingOrder.TotalAmount}).\n\n" +
                    (pendingPayUrl != null ? $"💳 Pay here: {pendingPayUrl}\n\n" : "") +
                    $"Complete the payment first, or wait for it to expire to get a new checkout link.");
                return;
            }

            await BotSendText(to, "🛒 Your cart is empty! Browse products first.");
            return;
        }

        // Re-check stock
        foreach (var item in cartItems)
        {
            if (item.Product.StockQuantity < item.Quantity)
            {
                await BotSendText(to, $"❌ Sorry, *{item.Product.Name}* only has {item.Product.StockQuantity} left in stock. Please update your cart.");
                return;
            }
        }

        // Pre-flight: verify we can generate a payment link BEFORE creating the order
        var baseUrl = GetPublicBaseUrl();
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.LogError("Cannot generate payment link — App:BaseUrl is not configured and RAILWAY_PUBLIC_DOMAIN is not set");
            await BotSendText(to, "❌ Sorry, we couldn't generate a payment link right now. Please contact us directly to complete your order.");
            customer.PendingAction = null;
            await _db.SaveChangesAsync();
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

        // Transactional Outbox: write the WhatsApp message to the DB in the SAME transaction
        // as the order. If the app crashes after commit, the background processor will find
        // this row and deliver it. Zero message loss.
        var outboxMessage = new WhatsAppOutboxMessage
        {
            To = to,
            Content = orderSummary,
            Context = $"Order confirmation for {order.OrderNumber}",
            Status = OutboxMessageStatus.Pending
        };
        _db.WhatsAppOutboxMessages.Add(outboxMessage);

        // Single atomic commit: order + stock reduction + cart clear + outbox message
        // If another concurrent request modified the same product (stock conflict),
        // DbUpdateConcurrencyException is thrown — see retry logic below.
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Stock concurrency conflict while placing order for customer {CustomerId}. Another order was competing for the same products.", customer.Id);
            // Detach conflicting entities and inform the customer to retry
            foreach (var entry in _db.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
            await BotSendText(to, "⚠️ Sorry, another order was placed at the same time for the same product. Please try placing your order again — your cart is still intact.");
            return;
        }

        // Try to send immediately (fast path — avoids waiting for the 10s poll cycle).
        // If this succeeds, mark the outbox row as Sent right away.
        // If rate limit or any error, the outbox row stays Pending → background processor retries.
        try
        {
            await BotSendText(to, orderSummary);

            // Immediate send succeeded — mark outbox as delivered
            outboxMessage.Status = OutboxMessageStatus.Sent;
            outboxMessage.SentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        catch (WhatsAppApiException ex)
        {
            _logger.LogWarning(ex,
                "Immediate send failed for {OrderNumber} to {Phone} — outbox message {OutboxId} will be retried by background processor",
                order.OrderNumber, to, outboxMessage.Id);
            // Outbox row stays Pending in DB — WhatsAppOutboxProcessor will pick it up
        }
    }

    /// <summary>
    /// Parses payload format: {prefix}{productId}_pi{imageId} or {prefix}{productId}
    /// Returns (productId, imageId) where imageId is null if not present, or 0 maps to null (primary).
    /// </summary>
    private static (int? productId, int? imageId) ParseProductImagePayload(string input, string prefix)
    {
        var remainder = input[prefix.Length..];

        // Check for _pi suffix: e.g. "3_pi16" or "3_pi0"
        var piIndex = remainder.IndexOf("_pi", StringComparison.Ordinal);
        if (piIndex >= 0)
        {
            var prodPart = remainder[..piIndex];
            var imgPart = remainder[(piIndex + 3)..]; // skip "_pi"
            if (int.TryParse(prodPart, out var prodId) && int.TryParse(imgPart, out var imgId))
            {
                // imgId 0 = primary image, store as null
                return (prodId, imgId == 0 ? null : imgId);
            }
            return (null, null);
        }

        // Legacy format: just productId
        if (int.TryParse(remainder, out var legacyProdId))
            return (legacyProdId, null);

        return (null, null);
    }

    /// <summary>Saves the customer's address and proceeds to place the order</summary>
    private async Task HandleAddressInput(string to, Customer customer, string address)
    {
        customer.Address = address;
        customer.PendingAction = null;
        await _db.SaveChangesAsync();

        try
        {
            await BotSendText(to, $"✅ Address saved:\n_{address}_\n\nProceeding to checkout...");
        }
        catch (WhatsAppApiException ex)
        {
            _logger.LogWarning(ex, "Failed to send address confirmation to {Phone}, continuing to place order", to);
        }

        await PlaceOrder(to, customer);
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
            await BotSendText(to, "📦 You don't have any orders yet. Start shopping!");
            return;
        }

        var sb = new StringBuilder();
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

        await BotSendText(to, sb.ToString());
    }
}
