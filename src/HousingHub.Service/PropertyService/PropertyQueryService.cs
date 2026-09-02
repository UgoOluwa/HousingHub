using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyAddress;
using HousingHub.Service.PropertyService.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyService;

public class PropertyQueryService : IPropertyQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly ILogger<PropertyQueryService> _logger;
    private readonly bool _usePublishedIndex;

    /// <summary>
    /// Whether a title-verified listing may display the "Title Verified" badge.
    /// </summary>
    /// <remarks>
    /// Off by default. The pipeline records title verification regardless — this only
    /// governs whether renters are shown the strongest claim the platform can make.
    /// It is a flag rather than a code change because the blocker is a lawyer
    /// reviewing the wording, not engineering.
    /// </remarks>
    private readonly bool _showTitleBadge;
    private const string ClassName = "property";

    public PropertyQueryService(
        IUnitOfWOrk unitOfWOrk,
        IMapper mapper,
        ILogger<PropertyQueryService> logger,
        IConfiguration? configuration = null)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
        _usePublishedIndex = configuration?.GetValue<bool>("Dynamo:UsePublishedIndex") ?? false;
        _showTitleBadge = configuration?.GetValue<bool>("Verification:ShowTitleBadge") ?? false;
    }

    /// <summary>
    /// Every published listing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the hottest read in the product — the homepage, the listing page, "new",
    /// "trending" and "nearby" all start here — and until now every one of them scanned
    /// the entire Properties table, because <c>IsPublished</c> is a plain bool with
    /// nothing to index against.
    /// </para>
    /// <para>
    /// <b>Behind a flag, and it must stay off until the backfill has run.</b>
    /// <c>PublishedStatus-index</c> is sparse: a row only appears in it once it has been
    /// written since the attribute was introduced. Listings that predate it are absent,
    /// so switching the read over early does not degrade the homepage — it empties it.
    /// Backfill first (a no-op re-save of every published property is enough), confirm
    /// the index count matches the published count, then set
    /// <c>Dynamo:UsePublishedIndex</c>. See docs/data-backfill-required.md.
    /// </para>
    /// </remarks>
    private Task<IEnumerable<Property>> GetPublishedPropertiesAsync() =>
        _usePublishedIndex
            ? _unitOfWOrk.PropertyQueries.GetAllAsync(x => x.PublishedStatus == Property.PublishedMarker)
            : _unitOfWOrk.PropertyQueries.GetAllAsync(x => x.IsPublished);

    /// <summary>
    /// Maps each listed property's owner to whether that owner is identity-verified.
    /// </summary>
    /// <remarks>
    /// One indexed read per distinct owner, issued together — a page of twenty
    /// listings by twenty different owners costs twenty parallel GetItems, not a scan
    /// of Customers. Deliberately not folded into the mapper: Mapster maps a Property,
    /// and this needs a second entity.
    /// </remarks>
    private async Task<Dictionary<Guid, OwnerVerification>> GetOwnerVerificationAsync(
        IEnumerable<Property> properties)
    {
        var ownerIds = properties.Select(p => p.OwnerId).Distinct().ToList();
        if (ownerIds.Count == 0) return [];

        var owners = await _unitOfWOrk.CustomerQueries.GetManyByAsync(c => c.Id, ownerIds);
        return owners.ToDictionary(o => o.Id, OwnerVerification.From);
    }

    /// <summary>
    /// The verification state of a listing's owner, as renters should see it.
    /// </summary>
    /// <remarks>
    /// Reads <c>IsBusinessVerified</c> rather than the stored tier, so a lapsed
    /// LASRERA permit stops showing a business badge the moment it expires rather
    /// than when the nightly sweep next runs. The sweep clears the stored value;
    /// this closes the window in between.
    /// </remarks>
    private readonly record struct OwnerVerification(bool IdentityVerified, VerificationTier Tier)
    {
        public static OwnerVerification From(Customer owner)
        {
            var tier = owner.IsBusinessVerified
                ? VerificationTier.BusinessVerified
                : owner.IsKycVerified
                    ? VerificationTier.IdentityVerified
                    : VerificationTier.Unverified;

            return new OwnerVerification(owner.IsKycVerified, tier);
        }
    }

    /// <summary>
    /// The strongest claim that can be made about a listing.
    /// </summary>
    /// <remarks>
    /// Takes the higher of the owner's tier and the property's own title tier,
    /// because they measure different things — an agent can be business-verified
    /// across ten listings while only one has had its title checked.
    ///
    /// Title is suppressed unless the badge has been cleared for display. Everything
    /// below it still shows, so a title-verified listing whose badge is switched off
    /// falls back to the owner's tier rather than showing nothing.
    /// </remarks>
    private VerificationTier ListingTierFor(Property property, VerificationTier ownerTier)
    {
        var titleTier = _showTitleBadge && property.IsTitleVerified
            ? VerificationTier.TitleVerified
            : VerificationTier.Unverified;

        return titleTier > ownerTier ? titleTier : ownerTier;
    }

    /// <summary>
    /// Maps properties to DTOs and stamps each with its owner's verification state.
    /// </summary>
    /// <remarks>
    /// Every public read path goes through here, so a new listing endpoint cannot
    /// silently ship without the badge — which is exactly how the flag came to be
    /// returned by the API and rendered by nothing.
    /// </remarks>
    private async Task<List<PropertyDto>> MapWithOwnerVerificationAsync(IReadOnlyCollection<Property> properties)
    {
        var verified = await GetOwnerVerificationAsync(properties);

        // Keyed by id so the entity's title state is reachable while mapping — the
        // DTO does not carry it, and the tier needs both halves.
        var byId = properties.ToDictionary(p => p.Id);

        return _mapper.Map<List<PropertyDto>>(properties)
            .Select(dto =>
            {
                var owner = verified.GetValueOrDefault(dto.OwnerId);

                return dto with
                {
                    IsOwnerVerified = owner.IdentityVerified,
                    OwnerVerificationTier = owner.Tier,
                    ListingVerificationTier = byId.TryGetValue(dto.Id, out var entity)
                        ? ListingTierFor(entity, owner.Tier)
                        : owner.Tier,
                };
            })
            .ToList();
    }

    public async Task<BaseResponse<PropertyDto?>> GetPropertyAsync(Guid id, Guid? requesterId = null, bool includeUnpublished = false)
    {
        try
        {
            Property? property = await _unitOfWOrk.PropertyQueries.GetByIdAsync(id);

            if (property is null)
                return new BaseResponse<PropertyDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (!includeUnpublished && !property.IsPublished && property.OwnerId != requesterId)
                return new BaseResponse<PropertyDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            // Only count views from clients, not the owner checking their own listing.
            if (property.OwnerId != requesterId)
            {
                property.ViewCount++;
                await _unitOfWOrk.PropertyCommands.UpdateAsync(property);
            }

            await AttachFilesAsync(property);

            var addressTask = _unitOfWOrk.PropertyAddressQueries.GetByAsync(a => a.PropertyId == property.Id);
            var ownerTask = _unitOfWOrk.CustomerQueries.GetByIdAsync(property.OwnerId);
            await Task.WhenAll(addressTask, ownerTask);

            var dto = _mapper.Map<PropertyDto>(property) with
            {
                PropertyAddress = addressTask.Result is { } address ? _mapper.Map<PropertyAddressDto>(address) : null,
                OwnerName = ownerTask.Result is { } owner ? $"{owner.FirstName} {owner.LastName}" : null,
                IsOwnerVerified = ownerTask.Result?.IsKycVerified == true,
                OwnerVerificationTier = ownerTask.Result is { } o
                    ? OwnerVerification.From(o).Tier
                    : VerificationTier.Unverified,
                ListingVerificationTier = ListingTierFor(
                    property,
                    ownerTask.Result is { } o2 ? OwnerVerification.From(o2).Tier : VerificationTier.Unverified)
            };

            return new BaseResponse<PropertyDto?>(dto, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyDto?>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<PropertyDto?>> GetPropertyByPropertyIdAsync(string propertyId)
    {
        try
        {
            Property? property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.PropertyId == propertyId);

            if (property is null)
                return new BaseResponse<PropertyDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            await AttachFilesAsync(property);

            var owner = await _unitOfWOrk.CustomerQueries.GetByIdAsync(property.OwnerId);
            var dto = _mapper.Map<PropertyDto>(property) with
            {
                IsOwnerVerified = owner?.IsKycVerified == true,
                OwnerVerificationTier = owner is null
                    ? VerificationTier.Unverified
                    : OwnerVerification.From(owner).Tier,
                ListingVerificationTier = ListingTierFor(
                    property,
                    owner is null ? VerificationTier.Unverified : OwnerVerification.From(owner).Tier)
            };

            return new BaseResponse<PropertyDto?>(dto, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyByPropertyIdAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyDto?>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetAllPropertiesAsync(bool includeUnpublished = false)
    {
        try
        {
            // Split rather than `includeUnpublished || x.IsPublished` in one predicate:
            // an OR can never be narrowed to an index, so the common case paid for the
            // rare one.
            var properties = (includeUnpublished
                ? await _unitOfWOrk.PropertyQueries.GetAllAsync()
                : await GetPublishedPropertiesAsync()).ToList();

            await AttachFilesAsync(properties);

            return new BaseResponse<List<PropertyDto>>(
                await MapWithOwnerVerificationAsync(properties), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<PaginatedResult<PropertyDto>>> GetAllPropertiesPaginatedAsync(GetAllPropertiesFilterDto filter)
    {
        try
        {
            var allProperties = await GetPublishedPropertiesAsync();
            var properties = allProperties.AsEnumerable();

            // Text search
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim().ToLower();
                properties = properties.Where(x => x.Title.ToLower().Contains(search));
            }

            // Features filter
            if (filter.Features.HasValue && filter.Features.Value != PropertyFeature.None)
            {
                properties = properties.Where(x => x.Features.HasFlag(filter.Features.Value));
            }

            // Property type filter
            if (filter.PropertyType.HasValue)
            {
                properties = properties.Where(x => x.PropertyType == filter.PropertyType.Value);
            }

            // Price range filter
            if (filter.MinPrice.HasValue)
            {
                properties = properties.Where(x => x.Price >= filter.MinPrice.Value);
            }
            if (filter.MaxPrice.HasValue)
            {
                properties = properties.Where(x => x.Price <= filter.MaxPrice.Value);
            }

            // Bedroom and bathroom filters.
            //
            // The filter fields have existed on GetAllPropertiesFilterDto since it was
            // written; the entity had nowhere to hold the answer, so this block sat
            // commented out and a request carrying ?bedrooms=3 was accepted, ignored, and
            // answered with every listing regardless. Silently returning the wrong set is
            // worse than rejecting the parameter.
            //
            // Exact match, not "at least": a search for a 3-bedroom flat is not satisfied
            // by a 6-bedroom one, at nearly double the rent.
            //
            // A listing whose owner never stated a count does not match either value, and
            // that is deliberate — every listing created before this field existed reads
            // as null, so filtering by bedrooms will exclude them until their owners edit
            // them. Matching them anyway would answer "3 bedrooms" with listings that have
            // made no such claim.
            if (filter.Bedrooms.HasValue)
            {
                properties = properties.Where(x => x.Bedrooms == filter.Bedrooms.Value);
            }

            if (filter.Bathrooms.HasValue)
            {
                properties = properties.Where(x => x.Bathrooms == filter.Bathrooms.Value);
            }

            // Location filter (by City/State)
            if (!string.IsNullOrWhiteSpace(filter.City) || !string.IsNullOrWhiteSpace(filter.State))
            {
                var propertyIds = properties.Select(p => p.Id).ToList();
                var addresses = await _unitOfWOrk.PropertyAddressQueries.GetManyByAsync(
                    a => a.PropertyId, propertyIds);

                var filteredAddresses = addresses.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(filter.City))
                {
                    var city = filter.City.Trim().ToLower();
                    filteredAddresses = filteredAddresses.Where(a => a.City.ToLower().Contains(city));
                }
                if (!string.IsNullOrWhiteSpace(filter.State))
                {
                    var state = filter.State.Trim().ToLower();
                    filteredAddresses = filteredAddresses.Where(a => a.State.ToLower().Contains(state));
                }

                var matchingAddressPropertyIds = filteredAddresses.Select(a => a.PropertyId).ToHashSet();
                properties = properties.Where(p => matchingAddressPropertyIds.Contains(p.Id));
            }

            var propertiesList = properties.ToList();
            var totalCount = propertiesList.Count;

            var paginatedProperties = propertiesList
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            await AttachFilesAsync(paginatedProperties);

            var mappedItems = await MapWithOwnerVerificationAsync(paginatedProperties);
            var paginatedResult = new PaginatedResult<PropertyDto>(mappedItems, totalCount, filter.PageNumber, filter.PageSize);

            return new BaseResponse<PaginatedResult<PropertyDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllPropertiesPaginatedAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<PropertyDto>>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetPropertiesByOwnerAsync(Guid ownerId)
    {
        try
        {
            var properties = (await _unitOfWOrk.PropertyQueries.GetAllAsync(
                x => x.OwnerId == ownerId)).ToList();

            await AttachFilesAsync(properties);

            return new BaseResponse<List<PropertyDto>>(
                await MapWithOwnerVerificationAsync(properties), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertiesByOwnerAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<PaginatedResult<PropertyDto>>> GetPropertiesByOwnerPaginatedAsync(Guid ownerId, GetMyPropertiesFilterDto filter)
    {
        try
        {
            var (properties, totalCount) = await _unitOfWOrk.PropertyQueries.GetPagedAsync(
                filter.PageNumber, filter.PageSize,
                predicate: x => x.OwnerId == ownerId);

            await AttachFilesAsync(properties);

            var propertyIds = properties.Select(p => p.Id).ToHashSet();
            // Status is not indexed, so it is filtered after the indexed read rather
            // than being folded into it. The index still does the work that matters —
            // bounding the read to this page's properties instead of the whole table.
            var inspections = await _unitOfWOrk.PropertyInspectionQueries.GetManyByAsync(
                i => i.PropertyId, propertyIds);
            var inspectionCountByProperty = inspections
                .Where(i => i.Status is InspectionStatus.Pending or InspectionStatus.Rescheduled)
                .GroupBy(i => i.PropertyId)
                .ToDictionary(g => g.Key, g => g.Count());

            // GetPagedAsync hands back IEnumerable; materialise once rather than
            // enumerating it again for the mapper.
            var mappedItems = (await MapWithOwnerVerificationAsync(properties.ToList()))
                .Select(dto => dto with { InspectionCount = inspectionCountByProperty.GetValueOrDefault(dto.Id, 0) })
                .ToList();
            var paginatedResult = new PaginatedResult<PropertyDto>(mappedItems, totalCount, filter.PageNumber, filter.PageSize);

            return new BaseResponse<PaginatedResult<PropertyDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertiesByOwnerPaginatedAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<PropertyDto>>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetNewPropertiesAsync(int count = 10)
    {
        try
        {
            var properties = await GetPublishedPropertiesAsync();

            var newProperties = properties
                .OrderByDescending(p => p.DateCreated)
                .Take(count)
                .ToList();

            await AttachFilesAsync(newProperties);

            return new BaseResponse<List<PropertyDto>>(
                await MapWithOwnerVerificationAsync(newProperties), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetNewPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetTrendingPropertiesAsync(int count = 10, int skip = 0)
    {
        try
        {
            var properties = await GetPublishedPropertiesAsync();

            var trending = properties
                .OrderByDescending(p => p.ViewCount)
                .Skip(skip)
                .Take(count)
                .ToList();

            await AttachFilesAsync(trending);

            return new BaseResponse<List<PropertyDto>>(
                await MapWithOwnerVerificationAsync(trending), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetTrendingPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetNearbyPropertiesAsync(double latitude, double longitude, double radiusKm = 10, int count = 10, int skip = 0)
    {
        try
        {
            var published = await GetPublishedPropertiesAsync();
            var properties = published.Where(p => p.Latitude.HasValue && p.Longitude.HasValue);

            var nearby = properties
                .Select(p => new
                {
                    Property = p,
                    Distance = HaversineDistanceKm(latitude, longitude, p.Latitude!.Value, p.Longitude!.Value)
                })
                .Where(x => x.Distance <= radiusKm)
                .OrderBy(x => x.Distance)
                .Skip(skip)
                .Take(count)
                .Select(x => x.Property)
                .ToList();

            await AttachFilesAsync(nearby);

            return new BaseResponse<List<PropertyDto>>(
                await MapWithOwnerVerificationAsync(nearby), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetNearbyPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    // PropertyFile lives in its own DynamoDB table ([DynamoDBIgnore] on Property.Files),
    // so it never comes back attached to a Property loaded from PropertyQueries — it must
    // be fetched and attached separately before mapping to PropertyDto.
    private async Task AttachFilesAsync(Property property)
    {
        var files = await _unitOfWOrk.PropertyFileQueries.GetAllAsync(x => x.PropertyId == property.Id);
        property.Files = files.ToList();
    }

    private async Task AttachFilesAsync(IEnumerable<Property> properties)
    {
        var propertyList = properties as IReadOnlyCollection<Property> ?? properties.ToList();
        if (propertyList.Count == 0)
            return;

        var propertyIds = propertyList.Select(p => p.Id).ToHashSet();
        var files = await _unitOfWOrk.PropertyFileQueries.GetManyByAsync(f => f.PropertyId, propertyIds);
        var filesByProperty = files.ToLookup(f => f.PropertyId);

        foreach (var property in propertyList)
            property.Files = filesByProperty[property.Id].ToList();
    }

    public async Task<BaseResponse<OwnerDashboardStatsDto>> GetOwnerDashboardStatsAsync(Guid ownerId)
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync(p => p.OwnerId == ownerId);
            var propertyList = properties.ToList();
            var propertyIds = propertyList.Select(p => p.Id).ToHashSet();

            int totalProperties = propertyList.Count;
            int activeListings = propertyList.Count(p => p.IsPublished && p.Availability == PropertyAvailability.Available);

            int pendingInspections = 0;
            int completedInspections = 0;

            if (propertyIds.Count > 0)
            {
                var inspections = await _unitOfWOrk.PropertyInspectionQueries.GetManyByAsync(
                    i => i.PropertyId, propertyIds);

                var inspectionList = inspections.ToList();
                pendingInspections = inspectionList.Count(i => i.Status == InspectionStatus.Pending);
                completedInspections = inspectionList.Count(i => i.Status == InspectionStatus.Completed);
            }

            var stats = new OwnerDashboardStatsDto(totalProperties, activeListings, pendingInspections, completedInspections);
            return new BaseResponse<OwnerDashboardStatsDto>(stats, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetOwnerDashboardStatsAsync: {Message}", ex.Message);
            return new BaseResponse<OwnerDashboardStatsDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }
}
