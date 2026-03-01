using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.DTOs.Product;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    /// <summary>All image URLs in display order (primary first, then additional).</summary>
    public List<string> ImageUrls { get; set; } = new();
    /// <summary>All images with IDs, in display order. Primary image has id=0.</summary>
    public List<ProductImageItemDto> ImageItems { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Image ID + URL pair for product image selection (e.g., carousel cards).</summary>
public class ProductImageItemDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class CreateProductDto
{
    [Required(ErrorMessage = "Product name is required.")]
    [MaxLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Brand is required.")]
    [MaxLength(100, ErrorMessage = "Brand cannot exceed 100 characters.")]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [MaxLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, 999999.99, ErrorMessage = "Price must be between 0.01 and 999999.99.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
    public int StockQuantity { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Additional image URLs (beyond the primary ImageUrl). Max 4 total.</summary>
    public List<string>? ImageUrls { get; set; }
}

public class UpdateProductDto
{
    [MaxLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
    public string? Name { get; set; }

    [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string? Description { get; set; }

    [MaxLength(100, ErrorMessage = "Brand cannot exceed 100 characters.")]
    public string? Brand { get; set; }

    [MaxLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
    public string? Category { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "Price must be between 0.01 and 999999.99.")]
    public decimal? Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
    public int? StockQuantity { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Additional image URLs (beyond the primary ImageUrl). Max 4 total. Pass empty list to clear.</summary>
    public List<string>? ImageUrls { get; set; }

    public bool? IsActive { get; set; }
}
