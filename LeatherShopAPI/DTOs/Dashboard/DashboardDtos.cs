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
    public List<OrderDto> RecentOrders { get; set; } = new();
}
