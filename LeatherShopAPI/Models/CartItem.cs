using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeatherShopAPI.Models;

public class CartItem
{
    [Key]
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; } = 1;

    /// <summary>Which ProductImage the customer selected from the carousel. Null = primary image.</summary>
    public int? SelectedImageId { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
