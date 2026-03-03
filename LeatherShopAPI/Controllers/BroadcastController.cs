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
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var history = await _broadcastService.GetHistoryAsync(page, pageSize, ct);
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
