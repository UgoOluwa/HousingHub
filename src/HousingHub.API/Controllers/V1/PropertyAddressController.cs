using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.PropertyAddress;
using HousingHub.Service.PropertyAddressService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[Controller]")]
public class PropertyAddressController : ControllerBase
{
    private readonly IPropertyAddressCommandService _commandService;
    private readonly IPropertyAddressQueryService _queryService;

    public PropertyAddressController(
        IPropertyAddressCommandService commandService,
        IPropertyAddressQueryService queryService)
    {
        _commandService = commandService;
        _queryService = queryService;
    }

    /// <summary>
    /// Address for a listing. Anonymous for published listings; unpublished ones are
    /// visible only to their owner, so the caller id is passed through when present.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<PropertyAddressDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _queryService.GetPropertyAddressAsync(id, GetAuthenticatedUserId());
        return Ok(response);
    }

    /// <inheritdoc cref="GetById" />
    [AllowAnonymous]
    [HttpGet("property/{propertyId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<PropertyAddressDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProperty(Guid propertyId)
    {
        var response = await _queryService.GetPropertyAddressByPropertyIdAsync(propertyId, GetAuthenticatedUserId());
        return Ok(response);
    }

    [Authorize(Policy = "PropertyOwnerOrAgent")]
    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<PropertyAddressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreatePropertyAddressDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var response = await _commandService.CreatePropertyAddress(request, userId.Value);
        return Ok(response);
    }

    /// <summary>
    /// The caller's id, or null when the request is anonymous. Anonymous is legitimate
    /// on the read endpoints above, so this returns null rather than throwing.
    /// </summary>
    private Guid? GetAuthenticatedUserId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                 ?? User.FindFirst(ClaimTypes.NameIdentifier);

        if (claim != null && Guid.TryParse(claim.Value, out var userId))
            return userId;

        return null;
    }
}
