using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.Brand)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Price)
            .HasColumnType("decimal(10,2)");

        builder.Property(p => p.ImageUrl)
            .HasMaxLength(500);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(p => p.Category);
        builder.HasIndex(p => p.Brand);
        builder.HasIndex(p => p.IsActive);

        // Seed data
        builder.HasData(
            new Product { Id = 1, Name = "Classic Leather Wallet", Brand = "Royal Leather", Category = "Wallet", Price = 899, StockQuantity = 50, Description = "Premium genuine leather wallet with multiple card slots", ImageUrl = "/images/wallet1.jpg" },
            new Product { Id = 2, Name = "Executive Leather Belt", Brand = "Royal Leather", Category = "Belt", Price = 1299, StockQuantity = 30, Description = "Formal leather belt with silver buckle", ImageUrl = "/images/belt1.jpg" },
            new Product { Id = 3, Name = "Leather Messenger Bag", Brand = "Heritage Craft", Category = "Bag", Price = 3499, StockQuantity = 20, Description = "Handcrafted messenger bag for daily use", ImageUrl = "/images/bag1.jpg" },
            new Product { Id = 4, Name = "Leather Oxford Shoes", Brand = "StepCraft", Category = "Shoes", Price = 4999, StockQuantity = 15, Description = "Premium leather formal shoes", ImageUrl = "/images/shoes1.jpg" },
            new Product { Id = 5, Name = "Leather Keychain", Brand = "Royal Leather", Category = "Accessories", Price = 299, StockQuantity = 100, Description = "Stylish leather keychain with metal ring", ImageUrl = "/images/keychain1.jpg" },
            new Product { Id = 6, Name = "Leather Laptop Sleeve", Brand = "Heritage Craft", Category = "Bag", Price = 2499, StockQuantity = 25, Description = "Slim leather sleeve for 15-inch laptops", ImageUrl = "/images/sleeve1.jpg" }
        );
    }
}
