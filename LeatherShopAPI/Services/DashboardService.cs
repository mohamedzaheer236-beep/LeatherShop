using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Dashboard;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;
using System.Globalization;

namespace LeatherShopAPI.Services;

public class DashboardService : IDashboardService
{
    private const int RecentOrdersCount = 10;
    private const int LowStockThreshold = 5;

    private static readonly string[] MonthLabels =
        CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames[..12];

    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var currentYear = now.Year;
        var currentMonth = now.Month;
        var lastMonth = now.AddMonths(-1);

        // Single query for all scalar statistics
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

        // Separate queries for cross-table counts
        var totalProducts = await _db.Products.CountAsync(p => p.IsActive, ct);
        var totalCustomers = await _db.Customers.CountAsync(ct);
        var lowStockProducts = await _db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= LowStockThreshold, ct);

        // Monthly revenue for current year
        var monthlyRaw = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt.Year == currentYear)
            .GroupBy(o => o.CreatedAt.Month)
            .Select(g => new
            {
                Month = g.Key,
                Revenue = g.Where(o => o.IsPaid).Sum(o => o.TotalAmount),
                OrderCount = g.Count()
            })
            .ToListAsync(ct);

        var monthlyRevenue = Enumerable.Range(1, 12)
            .Select(m =>
            {
                var data = monthlyRaw.FirstOrDefault(r => r.Month == m);
                return new MonthlyRevenueDto
                {
                    Month = m,
                    Label = MonthLabels[m - 1],
                    Revenue = data?.Revenue ?? 0,
                    OrderCount = data?.OrderCount ?? 0
                };
            })
            .ToList();

        // Order status distribution
        var ordersByStatus = await _db.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new OrderStatusCountDto
            {
                Status = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync(ct);

        // Growth: this month vs last month
        var thisMonthOrders = await _db.Orders
            .CountAsync(o => o.CreatedAt.Year == currentYear && o.CreatedAt.Month == currentMonth, ct);
        var lastMonthOrders = await _db.Orders
            .CountAsync(o => o.CreatedAt.Year == lastMonth.Year && o.CreatedAt.Month == lastMonth.Month, ct);

        var thisMonthRevenue = await _db.Orders
            .Where(o => o.IsPaid && o.CreatedAt.Year == currentYear && o.CreatedAt.Month == currentMonth)
            .SumAsync(o => o.TotalAmount, ct);
        var lastMonthRevenue = await _db.Orders
            .Where(o => o.IsPaid && o.CreatedAt.Year == lastMonth.Year && o.CreatedAt.Month == lastMonth.Month)
            .SumAsync(o => o.TotalAmount, ct);

        var thisMonthCustomers = await _db.Customers
            .CountAsync(c => c.CreatedAt.Year == currentYear && c.CreatedAt.Month == currentMonth, ct);
        var lastMonthCustomers = await _db.Customers
            .CountAsync(c => c.CreatedAt.Year == lastMonth.Year && c.CreatedAt.Month == lastMonth.Month, ct);

        // Recent orders with includes
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
            RevenueGrowth = CalcGrowth(thisMonthRevenue, lastMonthRevenue),
            OrderGrowth = CalcGrowth(thisMonthOrders, lastMonthOrders),
            CustomerGrowth = CalcGrowth(thisMonthCustomers, lastMonthCustomers),
            MonthlyRevenue = monthlyRevenue,
            OrdersByStatus = ordersByStatus,
            RecentOrders = recentOrders.Select(o => o.ToDto()).ToList()
        };
    }

    private static decimal CalcGrowth(decimal current, decimal previous)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return Math.Round((current - previous) / previous * 100, 1);
    }
}
