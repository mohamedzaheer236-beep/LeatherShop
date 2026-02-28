using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Data.Configurations;

public class BroadcastMessageConfiguration : IEntityTypeConfiguration<BroadcastMessage>
{
    public void Configure(EntityTypeBuilder<BroadcastMessage> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.MessageTemplate)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(b => b.MessageBody)
            .HasMaxLength(2000);

        // ─── DB-backed job data for restart survival ───

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(BroadcastStatus.Pending);

        builder.Property(b => b.LanguageCode)
            .HasMaxLength(10)
            .HasDefaultValue("en");

        builder.Property(b => b.ParametersJson)
            .HasColumnType("text");

        builder.Property(b => b.ImageUrl)
            .HasMaxLength(2000);

        builder.Property(b => b.RecipientsJson)
            .HasColumnType("text")
            .HasDefaultValue("[]");

        builder.Property(b => b.ProcessedPhonesJson)
            .HasColumnType("text")
            .HasDefaultValue("[]");

        // Background processor queries: WHERE Status IN ('Pending', 'Processing')
        builder.HasIndex(b => b.Status);
    }
}
