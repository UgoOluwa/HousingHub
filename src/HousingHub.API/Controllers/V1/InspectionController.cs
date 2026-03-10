using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Application.Commons.Bases;
using HousingHub.Application.Inspection.Commands.Cancel;
using HousingHub.Application.Inspection.Commands.Reschedule;
using HousingHub.Application.Inspection.Commands.Respond;
using HousingHub.Application.Inspection.Commands.RespondReschedule;
using HousingHub.Application.Inspection.Commands.Schedule;
using HousingHub.Application.Inspection.Queries.GetByCustomer;
using HousingHub.Application.Inspection.Queries.GetById;
using HousingHub.Application.Inspection.Queries.GetByProperty;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[Controller]")]
[Authorize]
public class InspectionController : ControllerBase
{
    private readonly IMediator _mediator;

    public InspectionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<InspectionDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Schedule(ScheduleInspectionCommand command)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var enrichedCommand = command with { AuthenticatedUserId = userId.Value };
        var response = await _mediator.Send(enrichedCommand);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<InspectionDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _mediator.Send(new GetInspectionByIdQuery(id));
        return Ok(response);
    }

    [HttpGet("property/{propertyId:guid}")]
    [ProducesResponseType(typeof(BaseResponsePagination<HousingHub.Core.CustomResponses.PaginatedResult<InspectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProperty(Guid propertyId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] InspectionStatus? status = null)
    {
        var response = await _mediator.Send(new GetInspectionsByPropertyQuery(propertyId, pageNumber, pageSize, status));
        return Ok(response);
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(BaseResponsePagination<HousingHub.Core.CustomResponses.PaginatedResult<InspectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyInspections([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] InspectionStatus? status = null)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new GetInspectionsByCustomerQuery(userId.Value, pageNumber, pageSize, status));
        return Ok(response);
    }

    [HttpPut("{id:guid}/respond")]
    [ProducesResponseType(typeof(BaseResponse<InspectionDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Respond(Guid id, RespondToInspectionCommand command)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var enrichedCommand = command with { InspectionId = id, AuthenticatedUserId = userId.Value };
        var response = await _mediator.Send(enrichedCommand);
        return Ok(response);
    }

    [HttpPut("{id:guid}/reschedule")]
    [ProducesResponseType(typeof(BaseResponse<InspectionDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reschedule(Guid id, RescheduleInspectionCommand command)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var enrichedCommand = command with { InspectionId = id, AuthenticatedUserId = userId.Value };
        var response = await _mediator.Send(enrichedCommand);
        return Ok(response);
    }

    [HttpPut("{id:guid}/respond-reschedule")]
    [ProducesResponseType(typeof(BaseResponse<InspectionDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RespondToReschedule(Guid id, [FromQuery] bool accept)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new RespondToRescheduleCommand(id, accept, userId.Value));
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new CancelInspectionCommand(id, userId.Value));
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
