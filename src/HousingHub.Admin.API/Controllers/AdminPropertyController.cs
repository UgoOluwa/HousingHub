using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.PropertyAddressService.Interfaces;
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
    ICustomerQueryService customerQueryService,
    IPropertyAddressQueryService propertyAddressQueryService) : ControllerBase
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
        // Owners are needed before filtering now, not just for display: the
        // UnverifiedOwnerOnly filter is a predicate on the owner, not the listing.
        var allTask = propertyQueryService.GetAllPropertiesAsync(includeUnpublished: true);
        var ownersTask = customerQueryService.GetAllCustomersAsync();
        await Task.WhenAll(allTask, ownersTask);

        var allResult = allTask.Result;
        var all = allResult.Data ?? [];
        var ownerMap = (ownersTask.Result.Data ?? []).ToDictionary(c => c.Id);

        bool OwnerIsVerified(Guid ownerId) =>
            ownerMap.TryGetValue(ownerId, out var o) && o.IsKycVerified;

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

        if (filter.FlaggedDuplicateOnly == true)
            query = query.Where(p => p.IsFlaggedDuplicate);

        // The backlog worklist: live listings that would not be publishable today.
        if (filter.UnverifiedOwnerOnly == true)
            query = query.Where(p => p.IsPublished && !OwnerIsVerified(p.OwnerId));

        var ordered = query.OrderByDescending(p => p.DateCreated).ToList();
        var totalCount = ordered.Count;
        var paged = ordered
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        // Enrich with owner names, addresses and inspection counts in parallel
        var ownerIds = paged.Select(p => p.OwnerId).Distinct().ToList();
        var propertyIds = paged.Select(p => p.Id).Distinct().ToList();

        var inspCountTask = inspectionQueryService.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(1, int.MaxValue));
        var addressTasks = propertyIds.ToDictionary(
            id => id,
            // Admins moderate unpublished listings, so they see those addresses too.
            id => propertyAddressQueryService.GetPropertyAddressByPropertyIdAsync(id, includeUnpublished: true));

        await Task.WhenAll(addressTasks.Values.Cast<Task>().Append(inspCountTask));

        var inspCountByProperty = (inspCountTask.Result.Data?.Items ?? [])
            .GroupBy(i => i.PropertyId)
            .ToDictionary(g => g.Key, g => g.Count());

        var items = paged.Select(p =>
        {
            ownerMap.TryGetValue(p.OwnerId, out var owner);
            var ownerName = owner != null ? $"{owner.FirstName} {owner.LastName}" : "N/A";
            var ownerVerified = owner?.IsKycVerified == true;

            var address = addressTasks[p.Id].Result.Data;
            var formattedAddress = address != null
                ? $"{address.Place}, {address.City}, {address.State}"
                : "N/A";

            var thumbnailUrl = p.Files?.OrderBy(f => f.DateUploaded).FirstOrDefault()?.FileUrl;
            var duplicateOfTitle = p.PossibleDuplicateOfPropertyId.HasValue
                ? all.FirstOrDefault(other => other.Id == p.PossibleDuplicateOfPropertyId.Value)?.Title
                : null;

            return new AdminPropertyListDto(
                p.Id,
                p.PropertyId,
                p.Title,
                ownerName,
                formattedAddress,
                p.DateCreated,
                p.IsPublished,
                p.PublishedAt,
                p.Availability,
                p.Price,
                inspCountByProperty.GetValueOrDefault(p.Id, 0),
                thumbnailUrl,
                p.IsFlaggedDuplicate,
                p.PossibleDuplicateOfPropertyId,
                duplicateOfTitle,
                ownerVerified,
                p.IsPublished && !ownerVerified);
        }).ToList();

        // Reflect the real outcome instead of always claiming success — a failure in
        // any of the underlying scans (properties/owners/inspections) previously
        // surfaced as an empty-but-"successful" list with no visible error.
        bool isSuccessful = allResult.IsSuccessful && ownersTask.Result.IsSuccessful && inspCountTask.Result.IsSuccessful;

        return Ok(new BaseResponse<PaginatedResult<AdminPropertyListDto>>(
            new PaginatedResult<AdminPropertyListDto>(items, totalCount, filter.PageNumber, filter.PageSize),
            isSuccessful, string.Empty, isSuccessful ? "Successful" : "One or more property lookups failed."));
    }

    /// <summary>Posts a new listing on behalf of an owner/agent HousingHub fully manages.</summary>
    /// <remarks>
    /// The target owner (<c>OwnerId</c> on the request) must exist, be a HouseOwner/Agent,
    /// and be flagged as managed by HousingHub (see <c>PUT /api/AdminOwner/{id}/managed</c>).
    /// Subject to the same possible-duplicate-address check as owner-created listings —
    /// if a similar listing already exists, this returns a warning instead of creating
    /// anything; resubmit with <c>ConfirmDuplicate=true</c> to proceed anyway.
    /// </remarks>
    /// <param name="request">Property details, including the target owner's ID.</param>
    /// <response code="200">Property created, or a possible-duplicate warning.</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BaseResponse<CreatePropertyResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromForm] CreatePropertyDto request)
    {
        var adminId = GetAdminId();
        var result = await propertyCommandService.CreateProperty(request, adminId, onBehalfOfOwnerId: request.OwnerId);
        if (!result.IsSuccessful) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Clears a property's possible-duplicate flag after review — the listing is legitimate, not a duplicate.</summary>
    /// <param name="id">Property's database ID.</param>
    /// <response code="200">Flag dismissed.</response>
    /// <response code="404">Property not found.</response>
    [HttpPut("{id:guid}/dismiss-duplicate-flag")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DismissDuplicateFlag(Guid id)
    {
        var result = await propertyCommandService.DismissDuplicateFlagAsync(id);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
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
        var result = await propertyQueryService.GetPropertyAsync(id, includeUnpublished: true);
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
    /// <param name="reason">Reason shown to the owner for why the listing was unpublished.</param>
    /// <response code="200">Property unpublished.</response>
    /// <response code="404">Property not found.</response>
    [HttpPut("{id:guid}/unpublish")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid id, [FromQuery] string reason)
    {
        var result = await propertyCommandService.SetPropertyPublishedAsync(id, false, reason);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Marks a property as verified.</summary>
    /// <param name="id">Property's database ID.</param>
    /// <response code="200">Property verified.</response>
    /// <response code="404">Property not found.</response>
    [HttpPut("{id:guid}/verify")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Verify(Guid id)
    {
        var result = await propertyCommandService.SetPropertyVerifiedAsync(id, true);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Removes a property's verified status.</summary>
    /// <param name="id">Property's database ID.</param>
    /// <response code="200">Property unverified.</response>
    /// <response code="404">Property not found.</response>
    [HttpPut("{id:guid}/unverify")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unverify(Guid id)
    {
        var result = await propertyCommandService.SetPropertyVerifiedAsync(id, false);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Permanently deletes a property (admin bypass — no ownership check).</summary>
    /// <param name="id">Property's database ID.</param>
    /// <param name="reason">Reason shown to the owner for why the listing was deleted.</param>
    /// <response code="204">Property deleted.</response>
    /// <response code="404">Property not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string reason)
    {
        var result = await propertyCommandService.AdminDeletePropertyAsync(id, reason);
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

    private Guid GetAdminId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                 ?? User.FindFirst(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
