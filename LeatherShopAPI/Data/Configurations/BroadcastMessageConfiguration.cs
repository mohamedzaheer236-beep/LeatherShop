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
    }
}
