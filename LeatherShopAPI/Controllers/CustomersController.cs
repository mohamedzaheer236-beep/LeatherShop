using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Customer;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? subscribedOnly, [FromQuery] string? search)
    {
        var customers = await _customerService.GetAllAsync(subscribedOnly, search);
        return Ok(ApiResponse<List<CustomerListDto>>.Ok(customers));
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetSubscriberCount()
    {
        var counts = await _customerService.GetCountAsync();
        return Ok(ApiResponse<CustomerCountDto>.Ok(counts));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
    {
        try
        {
            var result = await _customerService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id },
                ApiResponse<CustomerCreatedDto>.Ok(result, "Customer created successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> BulkImport([FromBody] BulkImportDto dto)
    {
        try
        {
            var result = await _customerService.BulkImportAsync(dto);
            return Ok(ApiResponse<BulkImportResultDto>.Ok(result, "Import completed."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/subscribe")]
    public async Task<IActionResult> ToggleSubscription(int id, [FromBody] bool isSubscribed)
    {
        var success = await _customerService.ToggleSubscriptionAsync(id, isSubscribed);
        if (!success)
            return NotFound(ApiResponse.Fail("Customer not found."));
        return Ok(ApiResponse.Ok("Subscription updated successfully."));
    }
}
