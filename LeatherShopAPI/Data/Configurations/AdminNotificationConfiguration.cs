using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Data.Configurations;

public class AdminNotificationConfiguration : IEntityTypeConfiguration<AdminNotification>
{
    public void Configure(EntityTypeBuilder<AdminNotification> builder)
    {
        // Primary query: fetch unread notifications ordered by most recent
        // WHERE IsRead = false ORDER BY CreatedAt DESC
        builder.HasIndex(n => new { n.IsRead, n.CreatedAt });

        // Cleanup query: delete old read notifications
        // WHERE IsRead = true AND CreatedAt < @cutoff
        builder.HasIndex(n => n.CreatedAt);

        builder.Property(n => n.Amount).HasColumnType("decimal(10,2)");
    }
}
