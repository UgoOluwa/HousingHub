using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>Manage inspections across the entire platform.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminInspectionController(
    IInspectionQueryService inspectionQueryService,
    IInspectionCommandService inspectionCommandService) : ControllerBase
{
    /// <summary>Returns a filtered, paginated list of all inspections on the platform.</summary>
    /// <remarks>
    /// Filter by status, scheduled date, property ID, or customer ID.
    /// Each item includes property name, address, customer name, scheduled date/time, and request date.
    /// </remarks>
    /// <param name="filter">Filter and pagination parameters.</param>
    /// <response code="200">Paginated inspection list.</response>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<AdminInspectionListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AdminInspectionFilterDto filter)
    {
        var result = await inspectionQueryService.GetAllInspectionsPaginatedAsync(filter);
        return Ok(result);
    }

    /// <summary>Returns full details of a single inspection.</summary>
    /// <param name="id">Inspection's database ID.</param>
    /// <response code="200">Inspection details.</response>
    /// <response code="404">Inspection not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<InspectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await inspectionQueryService.GetInspectionAsync(id);
        if (result.Data == null) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Confirms an inspection assignment on behalf of the property owner.</summary>
    /// <remarks>
    /// Admin can confirm pending inspections without requiring the owner to act.
    /// Uses a system admin ID as the authenticated user ID.
    /// </remarks>
    /// <param name="id">Inspection's database ID.</param>
    /// <response code="200">Inspection confirmed.</response>
    /// <response code="404">Inspection not found.</response>
    [HttpPut("{id:guid}/confirm")]
    [ProducesResponseType(typeof(BaseResponse<InspectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var adminId = GetAdminId();
        var dto = new RespondToInspectionDto(id, true, null);
        var result = await inspectionCommandService.RespondToInspectionAsync(dto, adminId);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Declines an inspection assignment on behalf of the property owner.</summary>
    /// <param name="id">Inspection's database ID.</param>
    /// <param name="declineNote">Optional reason for declining.</param>
    /// <response code="200">Inspection declined.</response>
    /// <response code="404">Inspection not found.</response>
    [HttpPut("{id:guid}/decline")]
    [ProducesResponseType(typeof(BaseResponse<InspectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Decline(Guid id, [FromQuery] string? declineNote = null)
    {
        var adminId = GetAdminId();
        var dto = new RespondToInspectionDto(id, false, declineNote);
        var result = await inspectionCommandService.RespondToInspectionAsync(dto, adminId);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Cancels an inspection.</summary>
    /// <param name="id">Inspection's database ID.</param>
    /// <response code="204">Inspection cancelled.</response>
    /// <response code="404">Inspection not found.</response>
    [HttpPut("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var adminId = GetAdminId();
        var result = await inspectionCommandService.CancelInspectionAsync(id, adminId);
        if (!result.IsSuccessful) return NotFound(result);
        return NoContent();
    }

    private Guid GetAdminId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                 ?? User.FindFirst(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
