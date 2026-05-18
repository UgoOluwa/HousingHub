using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Property;

namespace HousingHub.Service.PropertyService.Interfaces;

public interface IPropertyQueryService
{
    Task<BaseResponse<PropertyDto?>> GetPropertyAsync(Guid id);
    Task<BaseResponse<PropertyDto?>> GetPropertyByPropertyIdAsync(string propertyId);
    Task<BaseResponse<List<PropertyDto>>> GetAllPropertiesAsync();
    Task<BaseResponse<PaginatedResult<PropertyDto>>> GetAllPropertiesPaginatedAsync(GetAllPropertiesFilterDto filter);
    Task<BaseResponse<List<PropertyDto>>> GetPropertiesByOwnerAsync(Guid ownerId);
    Task<BaseResponse<PaginatedResult<PropertyDto>>> GetPropertiesByOwnerPaginatedAsync(Guid ownerId, GetMyPropertiesFilterDto filter);
    Task<BaseResponse<List<PropertyDto>>> GetNewPropertiesAsync(int count = 10);
    Task<BaseResponse<List<PropertyDto>>> GetTrendingPropertiesAsync(int count = 10);
    Task<BaseResponse<List<PropertyDto>>> GetNearbyPropertiesAsync(double latitude, double longitude, double radiusKm = 10, int count = 10);
    Task<BaseResponse<OwnerDashboardStatsDto>> GetOwnerDashboardStatsAsync(Guid ownerId);
}
