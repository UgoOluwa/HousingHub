using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Application.Chat.Commands.MarkAsRead;
using HousingHub.Application.Chat.Commands.SendMessage;
using HousingHub.Application.Chat.Queries.GetConversations;
using HousingHub.Application.Chat.Queries.GetMessages;
using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using BaseResponse = HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Chat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[Controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;

    public ChatController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("send")]
    [ProducesResponseType(typeof(HousingHub.Application.Commons.Bases.BaseResponse<ChatMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var command = new SendMessageCommand(request.RecipientId, request.Content, userId.Value);
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpGet("conversations")]
    [ProducesResponseType(typeof(HousingHub.Application.Commons.Bases.BaseResponse<List<ConversationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversations()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new GetConversationsQuery(userId.Value));
        return Ok(response);
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(BaseResponsePagination<PaginatedResult<ChatMessageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(Guid conversationId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new GetMessagesQuery(conversationId, userId.Value, pageNumber, pageSize));
        return Ok(response);
    }

    [HttpPut("conversations/{conversationId:guid}/read")]
    [ProducesResponseType(typeof(HousingHub.Application.Commons.Bases.BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsRead(Guid conversationId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new MarkConversationAsReadCommand(conversationId, userId.Value));
        return Ok(response);
    }

    private Guid? GetAuthenticatedUserId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                 ?? User.FindFirst(ClaimTypes.NameIdentifier);

        if (claim != null && Guid.TryParse(claim.Value, out var userId))
            return userId;

        return null;
    }
}

public record SendMessageRequest(Guid RecipientId, string Content);
