using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.TotalAmount)
            .HasColumnType("decimal(10,2)");

        builder.Property(o => o.PaymentId)
            .HasMaxLength(100);

        builder.Property(o => o.ShippingAddress)
            .HasMaxLength(500);

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();

        // Performance indexes for dashboard & filtering
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.IsPaid);
        builder.HasIndex(o => o.PaymentExpiresAt); // Used by ExpiredOrderCleanupService

        // Many-to-one: Order → Customer (Restrict: preserve order history for accounting)
        builder.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-many: Order → OrderItems
        builder.HasMany(o => o.OrderItems)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
