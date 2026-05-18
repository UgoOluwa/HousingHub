using Asp.Versioning;
using HousingHub.Service.Commons.Utilities;
using HousingHub.Service.Dtos.Utility;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[Controller]")]
public class UtilityController : ControllerBase
{
    private readonly IUtilityService _utilityService;

    public UtilityController(IUtilityService utilityService)
    {
        _utilityService = utilityService;
    }

    [HttpGet("enums")]
    [ProducesResponseType(typeof(Dictionary<string, List<EnumDetailDto>>), StatusCodes.Status200OK)]
    public IActionResult GetAllEnums()
    {
        var enums = _utilityService.GetAllEnums();
        return Ok(enums);
    }

    [HttpGet("enums/{enumName}")]
    [ProducesResponseType(typeof(List<EnumDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetEnum(string enumName)
    {
        var allEnums = _utilityService.GetAllEnums();
        if (!allEnums.TryGetValue(enumName, out var enumDetails))
            return NotFound(new { message = $"Enum '{enumName}' not found." });

        return Ok(enumDetails);
    }
}
