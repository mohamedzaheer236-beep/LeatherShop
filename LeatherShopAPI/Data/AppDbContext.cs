using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Models;
using System.Reflection;

namespace LeatherShopAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<BroadcastMessage> BroadcastMessages => Set<BroadcastMessage>();
    public DbSet<BroadcastRecipient> BroadcastRecipients => Set<BroadcastRecipient>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<WhatsAppOutboxMessage> WhatsAppOutboxMessages => Set<WhatsAppOutboxMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration classes from the Configurations folder
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Note: Product.RowVersion uses [Timestamp] which Npgsql automatically maps
        // to PostgreSQL's xmin system column as an optimistic concurrency token.
        // This prevents stock overselling when concurrent orders race on the same product.
    }
}
