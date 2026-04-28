using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.PropertyService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>Manage property listings.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminPropertyController(
    IPropertyQueryService propertyQueryService,
    IPropertyCommandService propertyCommandService,
    IInspectionQueryService inspectionQueryService,
    ICustomerQueryService customerQueryService) : ControllerBase
{
    /// <summary>Returns a filtered, paginated list of all properties.</summary>
    /// <remarks>
    /// Returns property name, owner name, address, date posted, published status and availability.
    /// Filter by publish state or availability; search by property title.
    /// </remarks>
    /// <param name="filter">Filter and pagination parameters.</param>
    /// <response code="200">Paginated property list.</response>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<AdminPropertyListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AdminPropertyFilterDto filter)
    {
        var allResult = await propertyQueryService.GetAllPropertiesAsync();
        var all = allResult.Data ?? [];

        IEnumerable<PropertyDto> query = all;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(p => p.Title.ToLowerInvariant().Contains(term));
        }

        if (filter.IsPublished.HasValue)
            query = query.Where(p => p.IsPublished == filter.IsPublished.Value);

        if (filter.Availability.HasValue)
            query = query.Where(p => p.Availability == filter.Availability.Value);

        var ordered = query.OrderByDescending(p => p.DateCreated).ToList();
        var totalCount = ordered.Count;
        var paged = ordered
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        // Enrich with owner names and addresses in parallel
        var ownerIds = paged.Select(p => p.OwnerId).Distinct().ToList();
        var propertyIds = paged.Select(p => p.Id).Distinct().ToList();

        var ownersTask = customerQueryService.GetAllCustomersAsync();
        var inspCountTask = inspectionQueryService.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(1, int.MaxValue));

        await Task.WhenAll(ownersTask, inspCountTask);

        var ownerMap = (ownersTask.Result.Data ?? []).ToDictionary(c => c.Id);
        var inspCountByProperty = (inspCountTask.Result.Data?.Items ?? [])
            .GroupBy(i => i.PropertyId)
            .ToDictionary(g => g.Key, g => g.Count());

        var items = paged.Select(p =>
        {
            ownerMap.TryGetValue(p.OwnerId, out var owner);
            var ownerName = owner != null ? $"{owner.FirstName} {owner.LastName}" : "N/A";
            return new AdminPropertyListDto(
                p.Id,
                p.PropertyId,
                p.Title,
                ownerName,
                $"Address ID: {p.AddressId}",
                p.DateCreated,
                p.IsPublished,
                p.PublishedAt,
                p.Availability,
                p.Price,
                inspCountByProperty.GetValueOrDefault(p.Id, 0));
        }).ToList();

        return Ok(new BaseResponse<PaginatedResult<AdminPropertyListDto>>(
            new PaginatedResult<AdminPropertyListDto>(items, totalCount, filter.PageNumber, filter.PageSize),
            true, string.Empty, "Successful"));
    }

    /// <summary>Returns full details of a single property.</summary>
    /// <param name="id">Property's database ID.</param>
    /// <response code="200">Property details.</response>
    /// <response code="404">Property not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<PropertyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await propertyQueryService.GetPropertyAsync(id);
        if (result.Data == null) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Publishes a property so it is visible to the public.</summary>
    /// <param name="id">Property's database ID.</param>
    /// <response code="200">Property published.</response>
    /// <response code="404">Property not found.</response>
    [HttpPut("{id:guid}/publish")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await propertyCommandService.SetPropertyPublishedAsync(id, true);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Unpublishes a property, hiding it from the public.</summary>
    /// <param name="id">Property's database ID.</param>
    /// <response code="200">Property unpublished.</response>
    /// <response code="404">Property not found.</response>
    [HttpPut("{id:guid}/unpublish")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var result = await propertyCommandService.SetPropertyPublishedAsync(id, false);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Permanently deletes a property (admin bypass — no ownership check).</summary>
    /// <param name="id">Property's database ID.</param>
    /// <response code="204">Property deleted.</response>
    /// <response code="404">Property not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await propertyCommandService.AdminDeletePropertyAsync(id);
        if (!result.IsSuccessful) return NotFound(result);
        return NoContent();
    }

    /// <summary>Returns a filtered, paginated list of inspections for a specific property.</summary>
    /// <param name="id">Property's database ID.</param>
    /// <param name="pageNumber">Page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="status">Optional inspection status filter.</param>
    /// <response code="200">Paginated inspection list.</response>
    [HttpGet("{id:guid}/inspections")]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<AdminInspectionListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPropertyInspections(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] InspectionStatus? status = null)
    {
        var result = await inspectionQueryService.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(pageNumber, pageSize, status, null, id, null));
        return Ok(result);
    }
}
