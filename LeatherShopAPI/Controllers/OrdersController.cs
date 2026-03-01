using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly InvoicePdfService _pdfService;

    public OrdersController(IOrderService orderService, InvoicePdfService pdfService)
    {
        _orderService = orderService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var result = await _orderService.GetAllAsync(status, page, pageSize);
        return Ok(ApiResponse<PaginatedResult<OrderDto>>.Ok(result));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string newStatus)
    {
        // Validate status string at controller level — return 400 for garbage input
        if (!Enum.TryParse<OrderStatus>(newStatus, true, out _))
            return BadRequest(ApiResponse.Fail($"Invalid status '{newStatus}'. Valid values: {string.Join(", ", Enum.GetNames<OrderStatus>())}."));

        var result = await _orderService.UpdateStatusAsync(id, newStatus);
        return result switch
        {
            UpdateStatusResult.NotFound => NotFound(ApiResponse.Fail("Order not found.")),
            UpdateStatusResult.InvalidStatus => BadRequest(ApiResponse.Fail($"Invalid status '{newStatus}'.")),
            UpdateStatusResult.InvalidTransition => BadRequest(ApiResponse.Fail($"Cannot transition to '{newStatus}' from the current order status.")),
            UpdateStatusResult.ConcurrencyConflict => Conflict(ApiResponse.Fail("Another operation modified this product's stock concurrently. Please retry.")),
            _ => Ok(ApiResponse.Ok("Order status updated successfully."))
        };
    }

    [HttpGet("{id}/invoice")]
    public async Task<IActionResult> DownloadInvoice(int id)
    {
        var order = await _orderService.GetByIdWithDetailsAsync(id);
        if (order is null)
            return NotFound(ApiResponse.Fail("Order not found."));

        var pdfBytes = _pdfService.GenerateInvoice(order);
        return File(pdfBytes, "application/pdf", $"Invoice-{order.Id}.pdf");
    }
}
