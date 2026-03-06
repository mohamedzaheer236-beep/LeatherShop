using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeatherShopAPI.DTOs.Chat;
using LeatherShopAPI.Models;
using Asp.Versioning;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/chat")]
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
    public async Task<IActionResult> GetConversations([FromQuery] string? search, CancellationToken ct)
    {
        var conversations = await _chatService.GetConversationsAsync(search, ct);
        return Ok(ApiResponse<List<ConversationDto>>.Ok(conversations));
    }

    /// <summary>Get chat messages for a customer (paginated, newest first).</summary>
    [HttpGet("{customerId:int}/messages")]
    public async Task<IActionResult> GetMessages(int customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var messages = await _chatService.GetMessagesAsync(customerId, page, pageSize, ct);
        return Ok(ApiResponse<PaginatedResult<ChatMessageDto>>.Ok(messages));
    }

    /// <summary>Admin sends a WhatsApp message to a customer.</summary>
    [HttpPost("{customerId:int}/send")]
    public async Task<IActionResult> SendMessage(int customerId, [FromBody] SendMessageDto dto, CancellationToken ct)
    {
        // Note: [Required] + [MinLength(1)] on SendMessageDto.Message + [ApiController] auto-validation
        // handles empty/null input. Manual check removed - model validation is sufficient.

        var message = await _chatService.SendMessageAsync(customerId, dto.Message, ct);
        return Ok(ApiResponse<ChatMessageDto>.Ok(message));
    }

    /// <summary>Toggle bot on/off for a customer.</summary>
    [HttpPost("{customerId:int}/toggle-bot")]
    public async Task<IActionResult> ToggleBot(int customerId, CancellationToken ct)
    {
        var isPaused = await _chatService.ToggleBotAsync(customerId, ct);
        if (isPaused == null)
            return NotFound(ApiResponse.Fail($"Customer {customerId} not found"));

        return Ok(ApiResponse<ToggleBotResponseDto>.Ok(
            new ToggleBotResponseDto { IsBotPaused = isPaused.Value },
            isPaused.Value ? "Bot paused" : "Bot resumed"));
    }

    /// <summary>Delete all chat messages for a customer.</summary>
    [HttpDelete("{customerId:int}/messages")]
    public async Task<IActionResult> DeleteConversation(int customerId, CancellationToken ct)
    {
        var deleted = await _chatService.DeleteConversationAsync(customerId, ct);
        if (!deleted)
            return NotFound(ApiResponse.Fail("No conversation found"));
        return Ok(ApiResponse.Ok("Conversation deleted"));
    }

    /// <summary>Get all permanently failed outbox messages (for admin follow-up).</summary>
    [HttpGet("failed-messages")]
    public async Task<IActionResult> GetFailedMessages(CancellationToken ct)
    {
        var messages = await _chatService.GetFailedOutboxMessagesAsync(ct);
        return Ok(ApiResponse<List<FailedOutboxMessageDto>>.Ok(messages));
    }

    /// <summary>Retry a permanently failed outbox message (resets to Pending for another round of attempts).</summary>
    [HttpPost("outbox/{id:int}/retry")]
    public async Task<IActionResult> RetryOutboxMessage(int id, CancellationToken ct)
    {
        var retried = await _chatService.RetryOutboxMessageAsync(id, ct);
        if (!retried)
            return NotFound(ApiResponse.Fail("Message not found or not in Failed state"));
        return Ok(ApiResponse.Ok("Message queued for retry"));
    }

    /// <summary>Get count of failed outbox messages (for badge display).</summary>
    [HttpGet("failed-messages/count")]
    public async Task<IActionResult> GetFailedMessageCount(CancellationToken ct)
    {
        var count = await _chatService.GetFailedOutboxCountAsync(ct);
        return Ok(ApiResponse<FailedMessageCountDto>.Ok(new FailedMessageCountDto { Count = count }));
    }
}
