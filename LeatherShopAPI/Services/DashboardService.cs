using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Dashboard;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class DashboardService : IDashboardService
{
    private const int RecentOrdersCount = 10;
    private const int LowStockThreshold = 5;

    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        // Single query for all scalar statistics - EF Core translates this into one SQL round-trip
        // using sub-selects, replacing 6 sequential COUNT/SUM calls.
        var stats = await _db.Orders
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                TotalRevenue = g.Where(o => o.IsPaid).Sum(o => o.TotalAmount),
                PendingOrders = g.Count(o => o.Status == OrderStatus.Pending)
            })
            .FirstOrDefaultAsync(ct);

        // Separate queries for cross-table counts (cannot be in the same GroupBy)
        var totalProducts = await _db.Products.CountAsync(p => p.IsActive, ct);
        var totalCustomers = await _db.Customers.CountAsync(ct);
        var lowStockProducts = await _db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= LowStockThreshold, ct);

        // Recent orders with includes - separate query (needs navigation properties)
        var recentOrders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.CreatedAt)
            .Take(RecentOrdersCount)
            .ToListAsync(ct);

        return new DashboardDto
        {
            TotalProducts = totalProducts,
            TotalCustomers = totalCustomers,
            TotalOrders = stats?.TotalOrders ?? 0,
            TotalRevenue = stats?.TotalRevenue ?? 0,
            PendingOrders = stats?.PendingOrders ?? 0,
            LowStockProducts = lowStockProducts,
            RecentOrders = recentOrders.Select(o => o.ToDto()).ToList()
        };
    }
}
