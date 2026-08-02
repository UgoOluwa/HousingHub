using HousingHub.Core.CustomResponses;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Dtos.Chat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.Admin.API.Controllers;

/// <summary>Messaging between an admin and customers/owners — shares the same Conversations/ChatMessages data as the consumer app's chat.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminChatController(
    IChatQueryService chatQueryService,
    IChatCommandService chatCommandService) : ControllerBase
{
    /// <summary>Returns all of the authenticated admin's conversations, most recent first.</summary>
    /// <response code="200">List of conversations.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(BaseResponse<List<ConversationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConversations()
    {
        var adminId = GetAdminId();
        if (adminId == Guid.Empty) return Unauthorized();

        var result = await chatQueryService.GetConversationsAsync(adminId);
        return Ok(result);
    }

    /// <summary>Returns a paginated, most-recent-first page of messages in a conversation.</summary>
    /// <param name="id">Conversation ID.</param>
    /// <param name="pageNumber">Page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <response code="200">Paginated message list.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("conversations/{id:guid}/messages")]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<ChatMessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var adminId = GetAdminId();
        if (adminId == Guid.Empty) return Unauthorized();

        var result = await chatQueryService.GetMessagesAsync(id, adminId, pageNumber, pageSize);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Sends a message from the authenticated admin to a customer/owner, creating the conversation if it doesn't already exist.</summary>
    /// <param name="dto">Recipient and message content.</param>
    /// <response code="200">Message sent.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("send")]
    [ProducesResponseType(typeof(BaseResponse<ChatMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var adminId = GetAdminId();
        if (adminId == Guid.Empty) return Unauthorized();

        var result = await chatCommandService.SendMessageAsync(dto, adminId);
        if (!result.IsSuccessful) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Marks every unread message in a conversation (sent by the other participant) as read.</summary>
    /// <param name="id">Conversation ID.</param>
    /// <response code="200">Messages marked as read.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPut("conversations/{id:guid}/read")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var adminId = GetAdminId();
        if (adminId == Guid.Empty) return Unauthorized();

        var result = await chatCommandService.MarkConversationAsReadAsync(id, adminId);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    private Guid GetAdminId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                 ?? User.FindFirst(JwtRegisteredClaimNames.Sub);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
