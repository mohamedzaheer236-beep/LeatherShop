using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using LeatherShopAPI.Services;
using Asp.Versioning;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiVersion("1.0")]
[Authorize]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class BroadcastController : ControllerBase
{
    private readonly IBroadcastService _broadcastService;
    private readonly IProductService _productService;

    public BroadcastController(IBroadcastService broadcastService, IProductService productService)
    {
        _broadcastService = broadcastService;
        _productService = productService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendBroadcast([FromBody] BroadcastRequestDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _broadcastService.SendBroadcastAsync(dto, ct);
            return Ok(ApiResponse<BroadcastResultDto>.Ok(result, "Broadcast sent successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetBroadcastStatus(int id, CancellationToken ct)
    {
        var status = await _broadcastService.GetBroadcastStatusAsync(id, ct);
        if (status == null)
            return NotFound(ApiResponse.Fail("Broadcast not found."));
        return Ok(ApiResponse<BroadcastHistoryDto>.Ok(status));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? templateSearch = null,
        [FromQuery] int? recipientsFilter = null,
        [FromQuery] int? sentFilter = null,
        [FromQuery] int? deliveredFilter = null,
        [FromQuery] int? readFilter = null,
        [FromQuery] int? failedFilter = null,
        [FromQuery] string? dateSearch = null,
        [FromQuery] int? timezoneOffset = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var history = await _broadcastService.GetHistoryAsync(page, pageSize, sortField, sortOrder, templateSearch, recipientsFilter, sentFilter, deliveredFilter, readFilter, failedFilter, dateSearch, timezoneOffset, ct);
        return Ok(ApiResponse<PaginatedResult<BroadcastHistoryDto>>.Ok(history));
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
    {
        var templates = await _broadcastService.GetTemplatesAsync(ct);
        return Ok(ApiResponse<List<WhatsAppTemplate>>.Ok(templates));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var totalSent = await _broadcastService.GetTotalSentCountAsync(ct);
        return Ok(ApiResponse<int>.Ok(totalSent));
    }

    [HttpGet("{id}/recipients")]
    public async Task<IActionResult> GetRecipients(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var result = await _broadcastService.GetRecipientsAsync(id, page, pageSize, status, ct);
        return Ok(ApiResponse<PaginatedResult<BroadcastRecipientDto>>.Ok(result));
    }

    [HttpGet("{id}/delivery-summary")]
    public async Task<IActionResult> GetDeliverySummary(int id, CancellationToken ct)
    {
        var summary = await _broadcastService.GetDeliverySummaryAsync(id, ct);
        if (summary == null)
            return NotFound(ApiResponse.Fail("Broadcast not found."));
        return Ok(ApiResponse<BroadcastDeliverySummaryDto>.Ok(summary));
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> RetryFailedRecipients(int id, CancellationToken ct)
    {
        try
        {
            var result = await _broadcastService.RetryFailedRecipientsAsync(id, ct);
            return Ok(ApiResponse<BroadcastRetryResultDto>.Ok(result, result.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Upload an image for broadcast carousel cards. Reuses product image pipeline (resize + compress).</summary>
    [HttpPost("upload-image")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file provided."));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("File size must be under 5 MB."));

        try
        {
            var relativePath = await _productService.UploadImageAsync(file, ct);
            return Ok(ApiResponse<string>.Ok(relativePath, "Image uploaded successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}
