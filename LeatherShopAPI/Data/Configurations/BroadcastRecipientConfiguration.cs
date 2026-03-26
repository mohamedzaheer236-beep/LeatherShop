using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Data.Configurations;

public class BroadcastRecipientConfiguration : IEntityTypeConfiguration<BroadcastRecipient>
{
    public void Configure(EntityTypeBuilder<BroadcastRecipient> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Phone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.WamId)
            .HasMaxLength(200);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(BroadcastDeliveryStatus.Queued);

        builder.Property(r => r.ErrorDetail)
            .HasMaxLength(1000);

        // FK to BroadcastMessage — cascade delete: when broadcast is deleted, all recipient records go too
        builder.HasOne(r => r.BroadcastMessage)
            .WithMany(b => b.Recipients)
            .HasForeignKey(r => r.BroadcastMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for webhook status matching: find recipient by wamid (most common query)
        builder.HasIndex(r => r.WamId)
            .HasFilter("\"WamId\" IS NOT NULL");

        // Index for per-broadcast recipient listing
        builder.HasIndex(r => r.BroadcastMessageId);

        // Prevent duplicate: same phone in same broadcast
        builder.HasIndex(r => new { r.BroadcastMessageId, r.Phone })
            .IsUnique();

        // Default RetryCount to 0
        builder.Property(r => r.RetryCount)
            .HasDefaultValue(0);

        // Index for retry service: find retryable failed recipients efficiently
        builder.HasIndex(r => r.NextRetryAt)
            .HasFilter("\"NextRetryAt\" IS NOT NULL");
    }
}
