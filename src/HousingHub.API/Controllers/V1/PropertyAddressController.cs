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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<PropertyAddressDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _queryService.GetPropertyAddressAsync(id);
        return Ok(response);
    }

    [HttpGet("property/{propertyId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<PropertyAddressDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProperty(Guid propertyId)
    {
        var response = await _queryService.GetPropertyAddressByPropertyIdAsync(propertyId);
        return Ok(response);
    }

    [Authorize(Policy = "PropertyOwnerOrAgent")]
    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<PropertyAddressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreatePropertyAddressDto request)
    {
        var response = await _commandService.CreatePropertyAddress(request);
        return Ok(response);
    }
}
