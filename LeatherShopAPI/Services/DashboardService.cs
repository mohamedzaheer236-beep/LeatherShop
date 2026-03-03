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
        // Sequential awaits — DbContext is NOT thread-safe so Task.WhenAll is not safe here.
        // First query materializes recent orders with includes; remaining queries are simple COUNT/SUM that execute in <1ms each.
        var recentOrders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.CreatedAt)
            .Take(RecentOrdersCount)
            .ToListAsync(ct);

        return new DashboardDto
        {
            TotalProducts = await _db.Products.CountAsync(p => p.IsActive, ct),
            TotalCustomers = await _db.Customers.CountAsync(ct),
            TotalOrders = await _db.Orders.CountAsync(ct),
            TotalRevenue = await _db.Orders.Where(o => o.IsPaid).SumAsync(o => o.TotalAmount, ct),
            PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct),
            LowStockProducts = await _db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= LowStockThreshold, ct),
            RecentOrders = recentOrders.Select(o => o.ToDto()).ToList()
        };
    }
}
