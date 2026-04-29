using HousingHub.Service.AdminService;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.InspectionService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>Platform-wide dashboard statistics and activity feeds.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminDashboardController(
    IAdminDashboardService dashboardService,
    IInspectionQueryService inspectionQueryService) : ControllerBase
{
    /// <summary>Returns aggregate platform statistics.</summary>
    /// <remarks>
    /// Includes total customers, owners, agents, pending KYC count,
    /// active listings, pending inspections and today's inspection count.
    /// </remarks>
    /// <response code="200">Statistics returned successfully.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(AdminDashboardStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var stats = await dashboardService.GetStatsAsync();
        return Ok(stats);
    }

    /// <summary>Returns today's scheduled inspections with full property and customer details.</summary>
    /// <param name="pageNumber">Page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <response code="200">Paginated list of today's inspections.</response>
    [HttpGet("inspections/today")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTodaysInspections(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await inspectionQueryService.GetTodaysInspectionsPaginatedAsync(pageNumber, pageSize);
        return Ok(result);
    }

    /// <summary>Returns recent platform activity (last 7 days).</summary>
    /// <param name="count">Maximum number of activity items to return (default 20).</param>
    /// <response code="200">List of recent activity events.</response>
    [HttpGet("activity")]
    [ProducesResponseType(typeof(List<AdminRecentActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentActivity([FromQuery] int count = 20)
    {
        var result = await inspectionQueryService.GetRecentActivityAsync(count);
        return Ok(result);
    }
}
