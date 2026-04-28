using HousingHub.Model.Enums;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.PropertyService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>Platform-wide dashboard statistics and activity feeds.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminDashboardController(
    ICustomerQueryService customerQueryService,
    IPropertyQueryService propertyQueryService,
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
        var customersTask = customerQueryService.GetAllCustomersAsync();
        var propertiesTask = propertyQueryService.GetAllPropertiesAsync();
        var inspectionsTask = inspectionQueryService.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(1, int.MaxValue));

        await Task.WhenAll(customersTask, propertiesTask, inspectionsTask);

        var customers = customersTask.Result.Data ?? [];
        var properties = propertiesTask.Result.Data ?? [];
        var inspections = inspectionsTask.Result.Data?.Items ?? [];

        var today = DateTime.UtcNow.Date;

        var stats = new AdminDashboardStatsDto(
            TotalCustomers: customers.Count(c => (CustomerType)c.CustomerType == CustomerType.Customer),
            TotalOwners: customers.Count(c => ((CustomerType)c.CustomerType).HasFlag(CustomerType.HouseOwner)),
            TotalAgents: customers.Count(c => ((CustomerType)c.CustomerType).HasFlag(CustomerType.Agent)),
            PendingKyc: customers.Count(c => !((CustomerType)c.CustomerType).HasFlag(CustomerType.Admin)
                                          && !c.DateModified.Equals(default)),  // proxy: IsKycVerified not on CustomerDto
            ActiveListings: properties.Count(p => p.IsPublished && p.Availability == PropertyAvailability.Available),
            TotalProperties: properties.Count,
            PendingInspections: inspections.Count(i => i.Status == InspectionStatus.Pending),
            TodaysInspections: inspections.Count(i => i.ScheduledDate.Date == today));

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
