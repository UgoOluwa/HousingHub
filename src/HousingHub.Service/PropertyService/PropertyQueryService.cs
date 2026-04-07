using AutoMapper;
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
            System.Linq.Expressions.Expression<Func<Property, bool>>? predicate = null;

            var hasSearch = !string.IsNullOrWhiteSpace(filter.Search);
            var hasFeature = filter.Features.HasValue && filter.Features.Value != PropertyFeature.None;

            if (hasSearch && hasFeature)
            {
                var search = filter.Search!.Trim();
                var features = filter.Features!.Value;
                predicate = x => x.Title.Contains(search) && x.Features.HasFlag(features);
            }
            else if (hasSearch)
            {
                var search = filter.Search!.Trim();
                predicate = x => x.Title.Contains(search);
            }
            else if (hasFeature)
            {
                var features = filter.Features!.Value;
                predicate = x => x.Features.HasFlag(features);
            }

            var (properties, totalCount) = await _unitOfWOrk.PropertyQueries.GetPagedAsync(
                filter.PageNumber, filter.PageSize,
                predicate: predicate);

            var mappedItems = _mapper.Map<List<PropertyDto>>(properties);
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

    public async Task<BaseResponse<List<PropertyDto>>> GetTrendingPropertiesAsync(int count = 10)
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync();

            var trending = properties
                .OrderByDescending(p => p.ViewCount)
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

    public async Task<BaseResponse<List<PropertyDto>>> GetNearbyPropertiesAsync(double latitude, double longitude, double radiusKm = 10, int count = 10)
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
}
