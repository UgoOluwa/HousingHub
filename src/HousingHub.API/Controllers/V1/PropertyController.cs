using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Application.Commons.Bases;
using HousingHub.Application.Property.Commands.Create;
using HousingHub.Application.Property.Commands.Delete;
using HousingHub.Application.Property.Commands.DeleteFile;
using HousingHub.Application.Property.Commands.Update;
using HousingHub.Application.Property.Commands.UploadFiles;
using HousingHub.Application.Property.Queries.GetAll;
using HousingHub.Application.Property.Queries.GetById;
using HousingHub.Application.Property.Queries.GetFiles;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyFile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[Controller]")]
public class PropertyController : ControllerBase
{
    private readonly IMediator _mediator;

    public PropertyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(BaseResponsePagination<HousingHub.Core.CustomResponses.PaginatedResult<PropertyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] GetAllPropertiesFilterDto filter)
    {
        var response = await _mediator.Send(new GetAllPropertiesQuery(filter));
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<PropertyDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _mediator.Send(new GetPropertyByIdQuery(id));
        return Ok(response);
    }

    [Authorize(Policy = "PropertyOwnerOrAgent")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BaseResponse<PropertyDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromForm] CreatePropertyCommand command)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var enrichedCommand = command with { OwnerId = userId.Value };
        var response = await _mediator.Send(enrichedCommand);
        return Ok(response);
    }

    [Authorize(Policy = "PropertyOwnerOrAgent")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<PropertyDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, UpdatePropertyCommand command)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var enrichedCommand = command with { Id = id, AuthenticatedUserId = userId.Value };
        var response = await _mediator.Send(enrichedCommand);
        return Ok(response);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new DeletePropertyCommand(id, userId.Value));
        return Ok(response);
    }

    // ??? Property Files ??????????????????????????????????????????????

    [HttpGet("{id:guid}/files")]
    [ProducesResponseType(typeof(BaseResponse<List<PropertyFileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFiles(Guid id)
    {
        var response = await _mediator.Send(new GetPropertyFilesQuery(id));
        return Ok(response);
    }

    [Authorize]
    [HttpPost("{id:guid}/files")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BaseResponse<List<PropertyFileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadFiles(Guid id, [FromForm] List<IFormFile> files)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new UploadPropertyFilesCommand(id, userId.Value, files));
        return Ok(response);
    }

    [Authorize]
    [HttpDelete("{propertyId:guid}/files/{fileId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteFile(Guid propertyId, Guid fileId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _mediator.Send(new DeletePropertyFileCommand(fileId, userId.Value));
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
