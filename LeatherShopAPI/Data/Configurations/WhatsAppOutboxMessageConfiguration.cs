using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Data.Configurations;

public class WhatsAppOutboxMessageConfiguration : IEntityTypeConfiguration<WhatsAppOutboxMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppOutboxMessage> builder)
    {
        // Status stored as readable string ("Pending", "Sent", "Failed")
        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(10);

        // Primary query: background processor fetches Pending messages due for retry
        // WHERE Status = 'Pending' AND (NextRetryAt IS NULL OR NextRetryAt <= @now)
        builder.HasIndex(m => new { m.Status, m.NextRetryAt });

        // Admin dashboard: view messages sorted by creation time
        builder.HasIndex(m => m.CreatedAt);
    }
}
