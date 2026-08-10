using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.CustomerAddressService.Interfaces;
using HousingHub.Service.Dtos.CustomerAddress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[Controller]")]
[Authorize]
public class CustomerAddressController : ControllerBase
{
    private readonly ICustomerAddressCommandService _commandService;
    private readonly ICustomerAddressQueryService _queryService;

    public CustomerAddressController(
        ICustomerAddressCommandService commandService,
        ICustomerAddressQueryService queryService)
    {
        _commandService = commandService;
        _queryService = queryService;
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(BaseResponse<CustomerAddressDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAddress()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var response = await _queryService.GetCustomerAddressByCustomerIdAsync(userId.Value);
        return Ok(response);
    }

    /// <summary>Fetches a stored address by its id. Callers may only read their own.</summary>
    /// <remarks>
    /// Previously unguarded, so enumerating address GUIDs returned other users'
    /// residential addresses. Returns 404 rather than 403 on a mismatch so the
    /// endpoint doesn't confirm which ids exist.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<CustomerAddressDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var response = await _queryService.GetAddressAsync(id);

        if (response?.Data is null || response.Data.CustomerId != userId.Value)
            return NotFound();

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreateCustomerAddressDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized();

        var enriched = request with { CustomerId = userId.Value };
        var response = await _commandService.CreateCustomerAddress(enriched);
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
