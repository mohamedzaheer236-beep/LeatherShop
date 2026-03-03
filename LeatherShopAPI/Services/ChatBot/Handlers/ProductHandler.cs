using LeatherShopAPI.Data;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace LeatherShopAPI.Services.ChatBot.Handlers;

/// <summary>
/// Handles product listing, details display, and carousel/image sending.
/// </summary>
public class ProductHandler
{
    private readonly AppDbContext _db;
    private readonly BotMessageSender _bot;
    private readonly IConfiguration _config;
    private readonly ILogger<ProductHandler> _logger;

    public ProductHandler(AppDbContext db, BotMessageSender bot, IConfiguration config, ILogger<ProductHandler> logger)
    {
        _db = db;
        _bot = bot;
        _config = config;
        _logger = logger;
    }

    public async Task SendProductsInCategory(string to, string category, CancellationToken ct = default)
    {
        var products = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity > 0 && EF.Functions.ILike(p.Category, category))
            .OrderBy(p => p.Name)
            .Take(10) // WhatsApp list max 10 rows per section
            .ToListAsync(ct);

        if (!products.Any())
        {
            await _bot.SendText(to, $"No products found in '{category}'. Type *menu* to browse other categories.", ct);
            return;
        }

        var rows = products.Select(p => new ListRow
        {
            Id = $"prod_{p.Id}",
            Title = p.Name.Length > 24 ? p.Name[..24] : p.Name,
            Description = $"₹{p.Price} | {p.Brand} | Stock: {p.StockQuantity}"
        }).ToList();

        await _bot.SendList(
            to,
            headerText: $"🛍️ {char.ToUpper(category[0]) + category[1..]}",
            bodyText: $"Here are our {category} products. Tap to view details:",
            buttonText: "📦 View Products",
            sections: new List<ListSection>
            {
                new() { Title = $"{category} Products", Rows = rows }
            },
            ct: ct
        );
    }

    public async Task SendProductDetails(string to, int productId, CancellationToken ct = default)
    {
        var product = await _db.Products.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product == null)
        {
            await _bot.SendText(to, "Product not found. Type *menu* to browse.", ct);
            return;
        }

        var details = BuildProductDetailsText(product);

        // Build the full list of image URLs (primary first, then additional ordered)
        var imageUrls = new List<string>();
        var imageIds = new List<int>();
        if (!string.IsNullOrEmpty(product.ImageUrl))
        {
            imageUrls.Add(product.ImageUrl);
            imageIds.Add(0); // 0 = primary image
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
            var baseUrl = ChatBotHelpers.GetPublicBaseUrl(_config);
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogWarning("GetPublicBaseUrl() returned null — skipping image sends for product {ProductId}", productId);
            }
            else try
            {
                if (await TrySendCarousel(to, product, imageUrls, imageIds, baseUrl, ct))
                    return;

                await SendIndividualImages(to, product, details, imageUrls, baseUrl, ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send product images for product {ProductId}, falling back to text", productId);
            }
        }

        // Text fallback
        await SendProductDetailsButtons(to, product, details, ct);
    }

    public async Task SendProductDetailsText(string to, int productId, int? selectedImageId = null, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product == null)
        {
            await _bot.SendText(to, "Product not found. Type *menu* to browse.", ct);
            return;
        }

        var details = BuildProductDetailsText(product);
        var addCartPayload = selectedImageId.HasValue
            ? $"addcart_{product.Id}_pi{selectedImageId.Value}"
            : $"addcart_{product.Id}";

        var bodyText = details.Length > 1024 ? details[..1021] + "..." : details;
        await _bot.SendButtons(
            to,
            bodyText: bodyText,
            buttons: new List<ButtonOption>
            {
                new() { Id = addCartPayload, Title = "🛒 Add to Cart" },
                new() { Id = "browse_categories", Title = "🔙 Categories" },
                new() { Id = "main_menu", Title = "🏠 Main Menu" }
            },
            ct: ct
        );
    }

    private static string BuildProductDetailsText(Product product) =>
        $"*{product.Name}*\n\n" +
        $"🏷️ Brand: {product.Brand}\n" +
        $"📂 Category: {product.Category}\n" +
        $"💰 Price: ₹{product.Price}\n" +
        $"📦 In Stock: {product.StockQuantity}\n\n" +
        $"📝 {product.Description}";

    /// <summary>
    /// Attempts to send a carousel template. Returns true if successful.
    /// </summary>
    private async Task<bool> TrySendCarousel(string to, Product product,
        List<string> imageUrls, List<int> imageIds, string baseUrl, CancellationToken ct = default)
    {
        var carouselSupportedExts = new[] { ".jpg", ".jpeg", ".png" };
        var carouselImageUrls = imageUrls
            .Where(u => carouselSupportedExts.Contains(Path.GetExtension(u).ToLower()))
            .ToList();

        if (carouselImageUrls.Count < 2) return false;

        try
        {
            var cardCount = Math.Min(carouselImageUrls.Count, 4);
            var templateName = cardCount switch
            {
                2 => "product_gallery",
                3 => "product_gallery_3",
                _ => "product_gallery_4"
            };

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

            await _bot.SendCarousel(to, templateName, $"Browse {product.Name} images", cards, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Carousel template failed for product {ProductId}, falling back to individual images", product.Id);
            return false;
        }
    }

    private async Task SendIndividualImages(string to, Product product, string details,
        List<string> imageUrls, string baseUrl, CancellationToken ct = default)
    {
        var caption = details.Length > 1024 ? details[..1021] + "..." : details;

        for (int i = 0; i < imageUrls.Count; i++)
        {
            var url = imageUrls[i];
            var imageFullUrl = url.StartsWith("http") ? url : $"{baseUrl}{url}";
            var imgCaption = (i == 0) ? caption : null;

            _logger.LogInformation("Sending product image {Index}/{Total}: {FullUrl}",
                i + 1, imageUrls.Count, imageFullUrl);

            await _bot.SendImage(to, imageFullUrl, imgCaption, ct);
        }

        // Send action buttons separately (image messages don't support inline buttons)
        try
        {
            await _bot.SendButtons(
                to,
                bodyText: "What would you like to do?",
                buttons: new List<ButtonOption>
                {
                    new() { Id = $"addcart_{product.Id}", Title = "🛒 Add to Cart" },
                    new() { Id = "browse_categories", Title = "🔙 Categories" },
                    new() { Id = "main_menu", Title = "🏠 Main Menu" }
                },
                ct: ct
            );
        }
        catch (WhatsAppApiException ex)
        {
            _logger.LogWarning(ex, "Failed to send action buttons after product images (rate limit), product {ProductId}", product.Id);
        }
    }

    private async Task SendProductDetailsButtons(string to, Product product, string details, CancellationToken ct = default)
    {
        var bodyText = details.Length > 1024 ? details[..1021] + "..." : details;
        try
        {
            await _bot.SendButtons(
                to,
                bodyText: bodyText,
                buttons: new List<ButtonOption>
                {
                    new() { Id = $"addcart_{product.Id}", Title = "🛒 Add to Cart" },
                    new() { Id = "browse_categories", Title = "🔙 Categories" },
                    new() { Id = "main_menu", Title = "🏠 Main Menu" }
                },
                ct: ct
            );
        }
        catch (WhatsAppApiException ex)
        {
            _logger.LogWarning(ex, "Failed to send product detail buttons for product {ProductId}", product.Id);
        }
    }
}
