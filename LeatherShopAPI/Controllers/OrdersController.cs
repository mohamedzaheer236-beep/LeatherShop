using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var orders = await _orderService.GetAllAsync(status);
        return Ok(ApiResponse<List<OrderDto>>.Ok(orders));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string newStatus)
    {
        var success = await _orderService.UpdateStatusAsync(id, newStatus);
        if (!success)
            return NotFound(ApiResponse.Fail("Order not found."));
        return Ok(ApiResponse.Ok("Order status updated successfully."));
    }
}
