using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Dashboard;
using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var recentOrders = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CustomerName = o.Customer.Name,
                CustomerPhone = o.Customer.PhoneNumber,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                IsPaid = o.IsPaid,
                CreatedAt = o.CreatedAt,
                Items = o.OrderItems.Select(oi => new OrderItemDto
                {
                    ProductName = oi.Product.Name,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice
                }).ToList()
            }).ToListAsync();

        return new DashboardDto
        {
            TotalProducts = await _db.Products.CountAsync(p => p.IsActive),
            TotalCustomers = await _db.Customers.CountAsync(),
            TotalOrders = await _db.Orders.CountAsync(),
            TotalRevenue = await _db.Orders.Where(o => o.IsPaid).SumAsync(o => o.TotalAmount),
            PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending),
            LowStockProducts = await _db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= 5),
            RecentOrders = recentOrders
        };
    }
}
