using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyService;

public class PropertyQueryService : IPropertyQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly ILogger<PropertyQueryService> _logger;
    private const string ClassName = "property";

    public PropertyQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<PropertyQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<PropertyDto?>> GetPropertyAsync(Guid id)
    {
        try
        {
            Property? property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == id);

            if (property is null)
                return new BaseResponse<PropertyDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            property.ViewCount++;
            await _unitOfWOrk.PropertyCommands.UpdateAsync(property);

            return new BaseResponse<PropertyDto?>(_mapper.Map<PropertyDto>(property), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyDto?>(null, false, string.Empty, ex.Message);
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

            return new BaseResponse<PropertyDto?>(_mapper.Map<PropertyDto>(property), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyByPropertyIdAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyDto?>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetAllPropertiesAsync()
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync();

            return new BaseResponse<List<PropertyDto>>(
                _mapper.Map<List<PropertyDto>>(properties), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<PropertyDto>>> GetAllPropertiesPaginatedAsync(GetAllPropertiesFilterDto filter)
    {
        try
        {
            var allProperties = await _unitOfWOrk.PropertyQueries.GetAllAsync();
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

            // Location filter (by City/State)
            if (!string.IsNullOrWhiteSpace(filter.City) || !string.IsNullOrWhiteSpace(filter.State))
            {
                var propertyIds = properties.Select(p => p.Id).ToList();
                var addresses = await _unitOfWOrk.PropertyAddressQueries.GetAllAsync(
                    a => propertyIds.Contains(a.PropertyId));

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

            var mappedItems = _mapper.Map<List<PropertyDto>>(paginatedProperties);
            var paginatedResult = new PaginatedResult<PropertyDto>(mappedItems, totalCount, filter.PageNumber, filter.PageSize);

            return new BaseResponse<PaginatedResult<PropertyDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllPropertiesPaginatedAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<PropertyDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetPropertiesByOwnerAsync(Guid ownerId)
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync(
                x => x.OwnerId == ownerId);

            return new BaseResponse<List<PropertyDto>>(
                _mapper.Map<List<PropertyDto>>(properties), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertiesByOwnerAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<PropertyDto>>> GetPropertiesByOwnerPaginatedAsync(Guid ownerId, GetMyPropertiesFilterDto filter)
    {
        try
        {
            var (properties, totalCount) = await _unitOfWOrk.PropertyQueries.GetPagedAsync(
                filter.PageNumber, filter.PageSize,
                predicate: x => x.OwnerId == ownerId);

            var mappedItems = _mapper.Map<List<PropertyDto>>(properties);
            var paginatedResult = new PaginatedResult<PropertyDto>(mappedItems, totalCount, filter.PageNumber, filter.PageSize);

            return new BaseResponse<PaginatedResult<PropertyDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertiesByOwnerPaginatedAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<PropertyDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetNewPropertiesAsync(int count = 10)
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync();

            var newProperties = properties
                .OrderByDescending(p => p.DateCreated)
                .Take(count)
                .ToList();

            return new BaseResponse<List<PropertyDto>>(
                _mapper.Map<List<PropertyDto>>(newProperties), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetNewPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetTrendingPropertiesAsync(int count = 10, int skip = 0)
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync();

            var trending = properties
                .OrderByDescending(p => p.ViewCount)
                .Skip(skip)
                .Take(count)
                .ToList();

            return new BaseResponse<List<PropertyDto>>(
                _mapper.Map<List<PropertyDto>>(trending), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetTrendingPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetNearbyPropertiesAsync(double latitude, double longitude, double radiusKm = 10, int count = 10, int skip = 0)
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync(
                p => p.Latitude.HasValue && p.Longitude.HasValue);

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

            return new BaseResponse<List<PropertyDto>>(
                _mapper.Map<List<PropertyDto>>(nearby), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetNearbyPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ex.Message);
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

    public async Task<BaseResponse<OwnerDashboardStatsDto>> GetOwnerDashboardStatsAsync(Guid ownerId)
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync(p => p.OwnerId == ownerId);
            var propertyList = properties.ToList();
            var propertyIds = propertyList.Select(p => p.Id).ToHashSet();

            int totalProperties = propertyList.Count;
            int activeListings = propertyList.Count(p => p.Availability == PropertyAvailability.Available);

            int pendingInspections = 0;
            int completedInspections = 0;

            if (propertyIds.Count > 0)
            {
                var inspections = await _unitOfWOrk.PropertyInspectionQueries.GetAllAsync(
                    i => propertyIds.Contains(i.PropertyId));

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
            return new BaseResponse<OwnerDashboardStatsDto>(null, false, string.Empty, ex.Message);
        }
    }
}
