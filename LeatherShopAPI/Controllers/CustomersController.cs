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
            return Ok(ApiResponse<CustomerCreatedDto>.Ok(result, "Customer created successfully."));
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

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerDto dto)
    {
        var result = await _customerService.UpdateAsync(id, dto);
        if (result == null)
            return NotFound(ApiResponse.Fail("Customer not found."));
        return Ok(ApiResponse<CustomerListDto>.Ok(result, "Customer updated successfully."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _customerService.DeleteAsync(id);
        return result.Result switch
        {
            DeleteCustomerResult.NotFound => NotFound(ApiResponse.Fail("Customer not found.")),
            DeleteCustomerResult.HasOrders => Conflict(ApiResponse.Fail(
                $"Cannot delete customer with {result.OrderCount} order(s). " +
                "Order history is preserved for accounting. Consider unsubscribing them instead.")),
            _ => Ok(ApiResponse.Ok("Customer deleted successfully."))
        };
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
