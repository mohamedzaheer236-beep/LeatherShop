using LeatherShopAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeatherShopAPI.Data;

/// <summary>
/// Runtime data seeder - seeds initial admin user and sample products on startup
/// if the database is empty. Replaces HasData() configuration which pollutes every migration
/// with timestamp noise.
/// </summary>
public static class DataSeeder
{
    /// <summary>
    /// Seeds the database with initial data if tables are empty.
    /// Called once on startup after migrations have been applied.
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        var changed = false;

        // Seed default admin if no admin users exist
        if (!await db.AdminUsers.AnyAsync())
        {
            var adminPassword = config["Admin:SeedPassword"];
            if (string.IsNullOrWhiteSpace(adminPassword))
                throw new InvalidOperationException(
                    "Admin:SeedPassword is not configured but no admin users exist in the database. " +
                    "Set it in appsettings.Local.json or via Admin__SeedPassword environment variable.");

            db.AdminUsers.Add(new AdminUser
            {
                Username = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                CreatedAt = DateTime.UtcNow
            });
            changed = true;
            logger.LogInformation("Default admin user seeded");
        }

        // Seed sample products if none exist
        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product { Name = "Classic Leather Wallet", Brand = "Royal Leather", Category = "Wallet", Price = 899, StockQuantity = 50, Description = "Premium genuine leather wallet with multiple card slots", ImageUrl = "/images/wallet1.jpg" },
                new Product { Name = "Executive Leather Belt", Brand = "Royal Leather", Category = "Belt", Price = 1299, StockQuantity = 30, Description = "Formal leather belt with silver buckle", ImageUrl = "/images/belt1.jpg" },
                new Product { Name = "Leather Messenger Bag", Brand = "Heritage Craft", Category = "Bag", Price = 3499, StockQuantity = 20, Description = "Handcrafted messenger bag for daily use", ImageUrl = "/images/bag1.jpg" },
                new Product { Name = "Leather Oxford Shoes", Brand = "StepCraft", Category = "Shoes", Price = 4999, StockQuantity = 15, Description = "Premium leather formal shoes", ImageUrl = "/images/shoes1.jpg" },
                new Product { Name = "Leather Keychain", Brand = "Royal Leather", Category = "Accessories", Price = 299, StockQuantity = 100, Description = "Stylish leather keychain with metal ring", ImageUrl = "/images/keychain1.jpg" },
                new Product { Name = "Leather Laptop Sleeve", Brand = "Heritage Craft", Category = "Bag", Price = 2499, StockQuantity = 25, Description = "Slim leather sleeve for 15-inch laptops", ImageUrl = "/images/sleeve1.jpg" }
            );
            changed = true;
            logger.LogInformation("Sample products seeded ({Count} products)", 6);
        }

        if (changed)
            await db.SaveChangesAsync();
    }
}
