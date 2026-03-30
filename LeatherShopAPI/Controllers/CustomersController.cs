using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Customer;
using LeatherShopAPI.Models;
using Asp.Versioning;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiVersion("1.0")]
[Authorize]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? subscribedOnly,
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? name = null,
        [FromQuery] string? phone = null,
        [FromQuery] string? address = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] int? orderCountMin = null,
        [FromQuery] int? orderCountMax = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var result = await _customerService.GetAllAsync(subscribedOnly, search, category, page, pageSize,
            sortField, sortOrder, name, phone, address, dateFrom, dateTo, orderCountMin, orderCountMax, ct);
        return Ok(ApiResponse<PaginatedResult<CustomerListDto>>.Ok(result));
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetSubscriberCount(CancellationToken ct)
    {
        var counts = await _customerService.GetCountAsync(ct);
        return Ok(ApiResponse<CustomerCountDto>.Ok(counts));
    }

    [HttpGet("check-phone")]
    public async Task<IActionResult> CheckPhone([FromQuery] string phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return BadRequest(ApiResponse.Fail("Phone number is required."));

        var exists = await _customerService.PhoneExistsAsync(phone, ct);
        return Ok(ApiResponse<object>.Ok(new { exists }));
    }

    [HttpPost("check-phones")]
    public async Task<IActionResult> CheckPhones([FromBody] CheckPhonesRequestDto dto, CancellationToken ct)
    {
        if (dto.Phones == null || dto.Phones.Count == 0)
            return Ok(ApiResponse<object>.Ok(new { existing = Array.Empty<string>() }));

        var existing = await _customerService.CheckPhonesAsync(dto.Phones, ct);
        return Ok(ApiResponse<object>.Ok(new { existing }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _customerService.CreateAsync(dto, ct);
            return Ok(ApiResponse<CustomerCreatedDto>.Ok(result, "Customer created successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> BulkImport([FromBody] BulkImportDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _customerService.BulkImportAsync(dto, ct);
            return Ok(ApiResponse<BulkImportResultDto>.Ok(result, "Import completed."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerDto dto, CancellationToken ct)
    {
        var result = await _customerService.UpdateAsync(id, dto, ct);
        if (result == null)
            return NotFound(ApiResponse.Fail("Customer not found."));
        return Ok(ApiResponse<CustomerListDto>.Ok(result, "Customer updated successfully."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _customerService.DeleteAsync(id, ct);
        return result.Result switch
        {
            DeleteCustomerResult.NotFound => NotFound(ApiResponse.Fail("Customer not found.")),
            DeleteCustomerResult.HasOrders => Conflict(ApiResponse.Fail(
                $"Cannot delete customer with {result.OrderCount} order(s). " +
                "Order history is preserved for accounting. Consider unsubscribing them instead.")),
            _ => Ok(ApiResponse.Ok("Customer deleted successfully."))
        };
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequestDto dto, CancellationToken ct)
    {
        if (dto.Ids == null || dto.Ids.Count == 0)
            return BadRequest(ApiResponse.Fail("No customer IDs provided."));

        var result = await _customerService.BulkDeleteAsync(dto.Ids, ct);
        return Ok(ApiResponse<BulkDeleteResultDto>.Ok(result, result.Message));
    }

    [HttpPut("{id:int}/subscribe")]
    public async Task<IActionResult> ToggleSubscription(int id, [FromBody] bool isSubscribed, CancellationToken ct)
    {
        var success = await _customerService.ToggleSubscriptionAsync(id, isSubscribed, ct);
        if (!success)
            return NotFound(ApiResponse.Fail("Customer not found."));
        return Ok(ApiResponse.Ok("Subscription updated successfully."));
    }
}
