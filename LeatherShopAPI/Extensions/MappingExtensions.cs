using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.DTOs.Product;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Extensions;

/// <summary>
/// Extension methods for mapping entity models to DTOs.
/// Eliminates duplicate mapping logic across services.
/// </summary>
public static class MappingExtensions
{
    // ── Product ──

    public static ProductDto ToDto(this Product p)
    {
        // Build combined image list: primary image first, then additional images ordered by DisplayOrder
        var imageUrls = new List<string>();
        var imageItems = new List<DTOs.Product.ProductImageItemDto>();
        if (!string.IsNullOrEmpty(p.ImageUrl))
        {
            imageUrls.Add(p.ImageUrl);
            imageItems.Add(new DTOs.Product.ProductImageItemDto { Id = 0, Url = p.ImageUrl });
        }

        if (p.Images != null)
        {
            foreach (var img in p.Images.OrderBy(i => i.DisplayOrder))
            {
                imageUrls.Add(img.ImageUrl);
                imageItems.Add(new DTOs.Product.ProductImageItemDto { Id = img.Id, Url = img.ImageUrl });
            }
        }

        return new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Brand = p.Brand,
            Category = p.Category,
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            ImageUrl = p.ImageUrl,
            VideoUrl = p.VideoUrl,
            ImageUrls = imageUrls,
            ImageItems = imageItems,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt
        };
    }

    // ── Order ──

    public static OrderDto ToDto(this Order o) => new()
    {
        Id = o.Id,
        OrderNumber = o.OrderNumber,
        CustomerName = o.Customer?.Name ?? string.Empty,
        CustomerPhone = o.Customer?.PhoneNumber ?? string.Empty,
        TotalAmount = o.TotalAmount,
        Status = o.Status.ToString(),
        IsPaid = o.IsPaid,
        ShippingAddress = o.ShippingAddress,
        PaymentId = o.PaymentId,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt,
        Items = o.OrderItems.Select(oi => oi.ToDto()).ToList()
    };

    public static OrderItemDto ToDto(this OrderItem oi) => new()
    {
        ProductName = oi.Product?.Name ?? string.Empty,
        Quantity = oi.Quantity,
        UnitPrice = oi.UnitPrice,
        SelectedImageUrl = ResolveSelectedImageUrl(oi)
    };

    /// <summary>
    /// Resolves the display image URL for an order item:
    ///   - If SelectedImageId is set, look up the ProductImage by ID in the navigation.
    ///   - If not found (image deleted) or null, fall back to Product.ImageUrl (primary).
    /// </summary>
    private static string? ResolveSelectedImageUrl(OrderItem oi)
    {
        if (oi.SelectedImageId.HasValue && oi.Product?.Images != null)
        {
            var selectedImg = oi.Product.Images.FirstOrDefault(pi => pi.Id == oi.SelectedImageId.Value);
            if (selectedImg != null)
                return selectedImg.ImageUrl;
        }
        // Fallback to primary image
        return string.IsNullOrEmpty(oi.Product?.ImageUrl) ? null : oi.Product.ImageUrl;
    }
}
