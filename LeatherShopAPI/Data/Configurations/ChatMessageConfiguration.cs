using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Data.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasOne(cm => cm.Customer)
            .WithMany()
            .HasForeignKey(cm => cm.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index for fetching chat history per customer (sorted by time)
        builder.HasIndex(cm => new { cm.CustomerId, cm.Timestamp });

        // Index for listing conversations (last message per customer)
        builder.HasIndex(cm => cm.Timestamp);

        builder.Property(cm => cm.Direction)
            .HasConversion<string>()
            .HasMaxLength(10);
    }
}
