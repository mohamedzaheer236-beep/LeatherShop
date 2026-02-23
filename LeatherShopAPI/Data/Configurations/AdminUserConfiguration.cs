using LeatherShopAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LeatherShopAPI.Data.Configurations;

public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("AdminUsers");

        builder.HasIndex(a => a.Username).IsUnique();

        builder.Property(a => a.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.PasswordHash)
            .IsRequired();
    }
}
