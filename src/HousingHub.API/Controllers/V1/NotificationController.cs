using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Application.Commons.Bases;
using HousingHub.Application.Notification.Commands.MarkAllAsRead;
using HousingHub.Application.Notification.Commands.MarkAsRead;
using HousingHub.Application.Notification.Queries.GetAll;
using HousingHub.Application.Notification.Queries.GetUnreadCount;
using HousingHub.Service.Dtos.Notification;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[Controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(BaseResponsePagination<HousingHub.Core.CustomResponses.PaginatedResult<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] bool? unreadOnly = null)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new GetNotificationsQuery(userId.Value, pageNumber, pageSize, unreadOnly));
        return Ok(response);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(BaseResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new GetUnreadCountQuery(userId.Value));
        return Ok(response);
    }

    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new MarkNotificationAsReadCommand(id, userId.Value));
        return Ok(response);
    }

    [HttpPut("read-all")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new MarkAllNotificationsAsReadCommand(userId.Value));
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
