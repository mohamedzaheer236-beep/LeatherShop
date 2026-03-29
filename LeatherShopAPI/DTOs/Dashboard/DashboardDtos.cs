using LeatherShopAPI.DTOs.Order;

namespace LeatherShopAPI.DTOs.Dashboard;

public class DashboardDto
{
    public int TotalProducts { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public int PendingOrders { get; set; }
    public int LowStockProducts { get; set; }

    // Growth percentages (this month vs last month)
    public decimal RevenueGrowth { get; set; }
    public decimal OrderGrowth { get; set; }
    public decimal CustomerGrowth { get; set; }

    // Analytics
    public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
    public List<OrderStatusCountDto> OrdersByStatus { get; set; } = new();
    public List<OrderDto> RecentOrders { get; set; } = new();
}

public class MonthlyRevenueDto
{
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class OrderStatusCountDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}
