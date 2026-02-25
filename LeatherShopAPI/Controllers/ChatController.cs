using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>Get all conversations (customers with chat history).</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations([FromQuery] string? search)
    {
        var conversations = await _chatService.GetConversationsAsync(search);
        return Ok(new ApiResponse<List<ConversationDto>> { Success = true, Data = conversations });
    }

    /// <summary>Get chat messages for a customer (paginated, newest first).</summary>
    [HttpGet("{customerId:int}/messages")]
    public async Task<IActionResult> GetMessages(int customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var messages = await _chatService.GetMessagesAsync(customerId, page, pageSize);
        return Ok(new ApiResponse<PaginatedResult<ChatMessageDto>> { Success = true, Data = messages });
    }

    /// <summary>Admin sends a WhatsApp message to a customer.</summary>
    [HttpPost("{customerId:int}/send")]
    public async Task<IActionResult> SendMessage(int customerId, [FromBody] SendMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Message cannot be empty" });

        var message = await _chatService.SendMessageAsync(customerId, dto.Message);
        return Ok(new ApiResponse<ChatMessageDto> { Success = true, Data = message });
    }

    /// <summary>Toggle bot on/off for a customer.</summary>
    [HttpPost("{customerId:int}/toggle-bot")]
    public async Task<IActionResult> ToggleBot(int customerId)
    {
        var isPaused = await _chatService.ToggleBotAsync(customerId);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = isPaused ? "Bot paused" : "Bot resumed",
            Data = new { isBotPaused = isPaused }
        });
    }

    /// <summary>Delete all chat messages for a customer.</summary>
    [HttpDelete("{customerId:int}/messages")]
    public async Task<IActionResult> DeleteConversation(int customerId)
    {
        var deleted = await _chatService.DeleteConversationAsync(customerId);
        if (!deleted)
            return NotFound(new ApiResponse<object> { Success = false, Message = "No conversation found" });
        return Ok(new ApiResponse<object> { Success = true, Message = "Conversation deleted" });
    }
}
