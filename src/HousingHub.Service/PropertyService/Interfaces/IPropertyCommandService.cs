using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Property;

namespace HousingHub.Service.PropertyService.Interfaces;

public interface IPropertyCommandService
{
    Task<BaseResponse<PropertyDto>> CreateProperty(CreatePropertyDto request);
}
