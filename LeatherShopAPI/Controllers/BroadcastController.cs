using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BroadcastController : ControllerBase
{
    private readonly IBroadcastService _broadcastService;

    public BroadcastController(IBroadcastService broadcastService)
    {
        _broadcastService = broadcastService;
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
}
