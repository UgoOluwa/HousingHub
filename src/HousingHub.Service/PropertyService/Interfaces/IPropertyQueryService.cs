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
}
