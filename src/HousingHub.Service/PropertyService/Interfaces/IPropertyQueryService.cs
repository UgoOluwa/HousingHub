using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Property;

namespace HousingHub.Service.PropertyService.Interfaces;

public interface IPropertyQueryService
{
    Task<BaseResponse<PropertyDto?>> GetPropertyAsync(Guid id);
    Task<BaseResponse<List<PropertyDto>>> GetAllPropertiesAsync();
}
