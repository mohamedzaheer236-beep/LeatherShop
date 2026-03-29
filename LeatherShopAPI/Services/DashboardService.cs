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

    public async Task<DashboardDto> GetDashboardAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var hasRange = from.HasValue && to.HasValue;

        // Normalize to UTC start/end of day
        var rangeFrom = from?.Date.ToUniversalTime() ?? DateTime.MinValue;
        var rangeTo = to?.Date.AddDays(1).ToUniversalTime() ?? DateTime.MaxValue;

        // Base queryable — optionally filtered by date range
        IQueryable<Order> ordersQuery = _db.Orders.AsNoTracking();
        if (hasRange)
            ordersQuery = ordersQuery.Where(o => o.CreatedAt >= rangeFrom && o.CreatedAt < rangeTo);

        // Scalar statistics
        var stats = await ordersQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                TotalRevenue = g.Where(o => o.IsPaid).Sum(o => o.TotalAmount),
                PendingOrders = g.Count(o => o.Status == OrderStatus.Pending)
            })
            .FirstOrDefaultAsync(ct);

        // Products / customers — not date-filtered (always show totals)
        var totalProducts = await _db.Products.CountAsync(p => p.IsActive, ct);
        var totalCustomers = hasRange
            ? await _db.Customers.CountAsync(c => c.CreatedAt >= rangeFrom && c.CreatedAt < rangeTo, ct)
            : await _db.Customers.CountAsync(ct);
        var lowStockProducts = await _db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= LowStockThreshold, ct);

        // Monthly revenue — group by Year+Month within range (or current year default)
        IQueryable<Order> monthlyQuery = _db.Orders.AsNoTracking();
        int chartYear;
        if (hasRange)
        {
            monthlyQuery = monthlyQuery.Where(o => o.CreatedAt >= rangeFrom && o.CreatedAt < rangeTo);
            chartYear = rangeFrom.Year; // label header uses the start year
        }
        else
        {
            chartYear = now.Year;
            monthlyQuery = monthlyQuery.Where(o => o.CreatedAt.Year == chartYear);
        }

        var monthlyRaw = await monthlyQuery
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Revenue = g.Where(o => o.IsPaid).Sum(o => o.TotalAmount),
                OrderCount = g.Count()
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        List<MonthlyRevenueDto> monthlyRevenue;
        if (hasRange && rangeFrom.Year != rangeTo.AddDays(-1).Year)
        {
            // Multi-year range: use actual months as labels
            monthlyRevenue = monthlyRaw.Select(r => new MonthlyRevenueDto
            {
                Month = r.Month,
                Label = $"{MonthLabels[r.Month - 1]} {r.Year}",
                Revenue = r.Revenue,
                OrderCount = r.OrderCount
            }).ToList();
        }
        else
        {
            // Single year: 12-month grid
            monthlyRevenue = Enumerable.Range(1, 12).Select(m =>
            {
                var data = monthlyRaw.FirstOrDefault(r => r.Month == m);
                return new MonthlyRevenueDto
                {
                    Month = m,
                    Label = MonthLabels[m - 1],
                    Revenue = data?.Revenue ?? 0,
                    OrderCount = data?.OrderCount ?? 0
                };
            }).ToList();
        }

        // Order status distribution (within range)
        var ordersByStatus = await ordersQuery
            .GroupBy(o => o.Status)
            .Select(g => new OrderStatusCountDto { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        // Growth: this month vs last month (only when no date range filter)
        decimal revenueGrowth = 0, orderGrowth = 0, customerGrowth = 0;
        if (!hasRange)
        {
            var currentMonth = now.Month;
            var currentYear = now.Year;
            var lastMonth = now.AddMonths(-1);

            var thisMonthOrders = await _db.Orders.CountAsync(o => o.CreatedAt.Year == currentYear && o.CreatedAt.Month == currentMonth, ct);
            var lastMonthOrders = await _db.Orders.CountAsync(o => o.CreatedAt.Year == lastMonth.Year && o.CreatedAt.Month == lastMonth.Month, ct);
            var thisMonthRevenue = await _db.Orders.Where(o => o.IsPaid && o.CreatedAt.Year == currentYear && o.CreatedAt.Month == currentMonth).SumAsync(o => o.TotalAmount, ct);
            var lastMonthRevenue = await _db.Orders.Where(o => o.IsPaid && o.CreatedAt.Year == lastMonth.Year && o.CreatedAt.Month == lastMonth.Month).SumAsync(o => o.TotalAmount, ct);
            var thisMonthCustomers = await _db.Customers.CountAsync(c => c.CreatedAt.Year == currentYear && c.CreatedAt.Month == currentMonth, ct);
            var lastMonthCustomers = await _db.Customers.CountAsync(c => c.CreatedAt.Year == lastMonth.Year && c.CreatedAt.Month == lastMonth.Month, ct);

            revenueGrowth = CalcGrowth(thisMonthRevenue, lastMonthRevenue);
            orderGrowth = CalcGrowth(thisMonthOrders, lastMonthOrders);
            customerGrowth = CalcGrowth(thisMonthCustomers, lastMonthCustomers);
        }

        // Recent orders (within range)
        IQueryable<Order> recentQuery = _db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product);
        if (hasRange)
            recentQuery = recentQuery.Where(o => o.CreatedAt >= rangeFrom && o.CreatedAt < rangeTo);
        var recentOrders = await recentQuery
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
            RevenueGrowth = revenueGrowth,
            OrderGrowth = orderGrowth,
            CustomerGrowth = customerGrowth,
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
