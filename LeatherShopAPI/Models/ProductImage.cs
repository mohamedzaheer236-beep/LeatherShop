using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.Models;

public class ProductImage
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    [Required, MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// 0-based display order. Lower values appear first.
    /// </summary>
    public int DisplayOrder { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
}
