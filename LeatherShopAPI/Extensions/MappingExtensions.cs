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

    public static ProductDto ToDto(this Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Brand = p.Brand,
        Category = p.Category,
        Price = p.Price,
        StockQuantity = p.StockQuantity,
        ImageUrl = p.ImageUrl,
        IsActive = p.IsActive
    };

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
        CreatedAt = o.CreatedAt,
        Items = o.OrderItems.Select(oi => oi.ToDto()).ToList()
    };

    public static OrderItemDto ToDto(this OrderItem oi) => new()
    {
        ProductName = oi.Product?.Name ?? string.Empty,
        Quantity = oi.Quantity,
        UnitPrice = oi.UnitPrice
    };
}
