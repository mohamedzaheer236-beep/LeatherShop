using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Data.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.HasKey(ci => ci.Id);

        // Unique constraint: one item per product per customer
        builder.HasIndex(ci => new { ci.CustomerId, ci.ProductId })
            .IsUnique();

        // Many-to-one: CartItem → Customer
        builder.HasOne(ci => ci.Customer)
            .WithMany(c => c.CartItems)
            .HasForeignKey(ci => ci.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Many-to-one: CartItem → Product
        builder.HasOne(ci => ci.Product)
            .WithMany()
            .HasForeignKey(ci => ci.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
