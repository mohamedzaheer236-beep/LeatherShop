using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
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
    public async Task<IActionResult> SendBroadcast([FromBody] BroadcastRequestDto dto)
    {
        try
        {
            var result = await _broadcastService.SendBroadcastAsync(dto);
            return Ok(ApiResponse<BroadcastResultDto>.Ok(result, "Broadcast sent successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetBroadcastStatus(int id)
    {
        var status = await _broadcastService.GetBroadcastStatusAsync(id);
        if (status == null)
            return NotFound(ApiResponse.Fail("Broadcast not found."));
        return Ok(ApiResponse<BroadcastHistoryDto>.Ok(status));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var history = await _broadcastService.GetHistoryAsync();
        return Ok(ApiResponse<List<BroadcastHistoryDto>>.Ok(history));
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await _broadcastService.GetTemplatesAsync();
        return Ok(ApiResponse<object>.Ok(templates));
    }

    /// <summary>Upload an image for broadcast carousel cards. Reuses product image pipeline (resize + compress).</summary>
    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file provided."));

        try
        {
            var relativePath = await _productService.UploadImageAsync(file);
            return Ok(ApiResponse<string>.Ok(relativePath, "Image uploaded successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}
