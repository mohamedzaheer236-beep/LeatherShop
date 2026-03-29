using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Order;
using LeatherShopAPI.Models;
using Asp.Versioning;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiVersion("1.0")]
[Authorize]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IInvoicePdfService _pdfService;

    public OrdersController(IOrderService orderService, IInvoicePdfService pdfService)
    {
        _orderService = orderService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var result = await _orderService.GetAllAsync(status, page, pageSize, ct);
        return Ok(ApiResponse<PaginatedResult<OrderDto>>.Ok(result));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? customerName = null,
        [FromQuery] string? customerPhone = null,
        [FromQuery] string? orderNumber = null,
        [FromQuery] string? status = null,
        [FromQuery] string? dateSearch = null,
        [FromQuery] decimal? amountMin = null,
        [FromQuery] decimal? amountMax = null,
        [FromQuery] string? isPaid = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var result = await _orderService.GetHistoryAsync(page, pageSize, sortField, sortOrder,
            customerName, customerPhone, orderNumber, status, dateSearch, amountMin, amountMax, isPaid, ct);
        return Ok(ApiResponse<PaginatedResult<OrderDto>>.Ok(result));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto, CancellationToken ct)
    {
        var newStatus = dto.Status;
        // Validate status string at controller level - return 400 for garbage input
        if (!Enum.TryParse<OrderStatus>(newStatus, true, out var parsedStatus))
            return BadRequest(ApiResponse.Fail($"Invalid status '{newStatus}'. Valid values: {string.Join(", ", Enum.GetNames<OrderStatus>())}."));

        // Tracking number is required when marking as Shipped
        if (parsedStatus == OrderStatus.Shipped && string.IsNullOrWhiteSpace(dto.TrackingNumber))
            return BadRequest(ApiResponse.Fail("Tracking number is required when marking an order as Shipped."));

        var result = await _orderService.UpdateStatusAsync(id, dto, ct);
        return result switch
        {
            UpdateStatusResult.NotFound => NotFound(ApiResponse.Fail("Order not found.")),
            UpdateStatusResult.InvalidStatus => BadRequest(ApiResponse.Fail($"Invalid status '{newStatus}'.")),
            UpdateStatusResult.InvalidTransition => BadRequest(ApiResponse.Fail($"Cannot transition to '{newStatus}' from the current order status.")),
            UpdateStatusResult.ConcurrencyConflict => Conflict(ApiResponse.Fail("Another operation modified this product's stock concurrently. Please retry.")),
            _ => Ok(ApiResponse.Ok("Order status updated successfully."))
        };
    }

    [HttpPatch("{id}/tracking")]
    public async Task<IActionResult> UpdateTracking(int id, [FromBody] UpdateTrackingDto dto, CancellationToken ct)
    {
        var result = await _orderService.UpdateTrackingAsync(id, dto, ct);
        return result switch
        {
            UpdateTrackingResult.NotFound   => NotFound(ApiResponse.Fail("Order not found.")),
            UpdateTrackingResult.NotShipped => BadRequest(ApiResponse.Fail("Tracking can only be updated for Shipped orders.")),
            _                               => Ok(ApiResponse.Ok("Tracking info updated successfully."))
        };
    }

    [HttpGet("{id}/invoice")]
    public async Task<IActionResult> DownloadInvoice(int id, CancellationToken ct)
    {
        var order = await _orderService.GetByIdWithDetailsAsync(id, ct);
        if (order is null)
            return NotFound(ApiResponse.Fail("Order not found."));

        var pdfBytes = _pdfService.GenerateInvoice(order);
        return File(pdfBytes, "application/pdf", $"Invoice-{order.Id}.pdf");
    }
}
