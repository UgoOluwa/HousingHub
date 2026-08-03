using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyAlert;
using HousingHub.Service.PropertyAlertService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

/// <summary>Saved property searches — get notified when a newly published property matches.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class PropertyAlertController : ControllerBase
{
    private readonly IPropertyAlertPreferenceCommandService _commandService;
    private readonly IPropertyAlertPreferenceQueryService _queryService;

    public PropertyAlertController(IPropertyAlertPreferenceCommandService commandService, IPropertyAlertPreferenceQueryService queryService)
    {
        _commandService = commandService;
        _queryService = queryService;
    }

    /// <summary>Returns the authenticated customer's saved searches.</summary>
    /// <response code="200">List of saved searches.</response>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<List<PropertyAlertPreferenceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var result = await _queryService.GetByCustomerAsync(userId.Value);
        return Ok(result);
    }

    /// <summary>Saves the current search as a preference — notifies the customer when a matching property is published.</summary>
    /// <response code="200">Preference saved.</response>
    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<PropertyAlertPreferenceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreatePropertyAlertPreferenceDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var result = await _commandService.CreateAsync(userId.Value, request);
        return Ok(result);
    }

    /// <summary>Deletes a saved search.</summary>
    /// <param name="id">Preference's database ID.</param>
    /// <response code="200">Preference deleted.</response>
    /// <response code="404">Not found, or not owned by the caller.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var result = await _commandService.DeleteAsync(id, userId.Value);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
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
