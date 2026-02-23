using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.Name)
            .HasMaxLength(200);

        builder.Property(c => c.Address)
            .HasMaxLength(500);

        // Always send IsSubscribed in INSERT (don't rely on DB default)
        // The C# property defaults to true in the model class
        builder.Property(c => c.IsSubscribed)
            .IsRequired();

        builder.HasIndex(c => c.PhoneNumber)
            .IsUnique();

        // One-to-many: Customer → Orders
        builder.HasMany(c => c.Orders)
            .WithOne(o => o.Customer)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many: Customer → CartItems
        builder.HasMany(c => c.CartItems)
            .WithOne(ci => ci.Customer)
            .HasForeignKey(ci => ci.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
