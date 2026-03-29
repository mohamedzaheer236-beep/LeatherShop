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

        // Monthly revenue — generate continuous month sequence within range
        IQueryable<Order> monthlyQuery = _db.Orders.AsNoTracking();
        DateTime seqStart, seqEnd;
        if (hasRange)
        {
            monthlyQuery = monthlyQuery.Where(o => o.CreatedAt >= rangeFrom && o.CreatedAt < rangeTo);
            seqStart = new DateTime(rangeFrom.Year, rangeFrom.Month, 1);
            // rangeTo is already +1 day, so the user's last selected month:
            var userEnd = rangeTo.AddDays(-1);
            seqEnd = new DateTime(userEnd.Year, userEnd.Month, 1);
        }
        else
        {
            var chartYear = now.Year;
            monthlyQuery = monthlyQuery.Where(o => o.CreatedAt.Year == chartYear);
            seqStart = new DateTime(chartYear, 1, 1);
            seqEnd = new DateTime(chartYear, 12, 1);
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

        // Build continuous month buckets from seqStart to seqEnd
        var monthlyRevenue = new List<MonthlyRevenueDto>();
        var spanMultipleYears = seqStart.Year != seqEnd.Year;
        for (var cursor = seqStart; cursor <= seqEnd; cursor = cursor.AddMonths(1))
        {
            var data = monthlyRaw.FirstOrDefault(r => r.Year == cursor.Year && r.Month == cursor.Month);
            monthlyRevenue.Add(new MonthlyRevenueDto
            {
                Month = cursor.Month,
                Label = spanMultipleYears
                    ? $"{MonthLabels[cursor.Month - 1]} {cursor.Year}"
                    : MonthLabels[cursor.Month - 1],
                Revenue = data?.Revenue ?? 0,
                OrderCount = data?.OrderCount ?? 0
            });
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
