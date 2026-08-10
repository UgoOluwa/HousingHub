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
                OwnerName = ownerTask.Result is { } owner ? $"{owner.FirstName} {owner.LastName}" : null
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

            return new BaseResponse<PropertyDto?>(_mapper.Map<PropertyDto>(property), true, string.Empty, ResponseMessages.Successful);
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
            var properties = includeUnpublished
                ? await _unitOfWOrk.PropertyQueries.GetAllAsync()
                : await GetPublishedPropertiesAsync();

            await AttachFilesAsync(properties);

            return new BaseResponse<List<PropertyDto>>(
                _mapper.Map<List<PropertyDto>>(properties), true, string.Empty, ResponseMessages.Successful);
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

            // Bedrooms filter (requires Bedrooms field on Property entity when added)
            // if (filter.Bedrooms.HasValue)
            // {
            //     properties = properties.Where(x => x.Bedrooms == filter.Bedrooms.Value);
            // }

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

            var mappedItems = _mapper.Map<List<PropertyDto>>(paginatedProperties);
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
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync(
                x => x.OwnerId == ownerId);

            await AttachFilesAsync(properties);

            return new BaseResponse<List<PropertyDto>>(
                _mapper.Map<List<PropertyDto>>(properties), true, string.Empty, ResponseMessages.Successful);
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

            var mappedItems = _mapper.Map<List<PropertyDto>>(properties)
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
                _mapper.Map<List<PropertyDto>>(newProperties), true, string.Empty, ResponseMessages.Successful);
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
                _mapper.Map<List<PropertyDto>>(trending), true, string.Empty, ResponseMessages.Successful);
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
                _mapper.Map<List<PropertyDto>>(nearby), true, string.Empty, ResponseMessages.Successful);
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
