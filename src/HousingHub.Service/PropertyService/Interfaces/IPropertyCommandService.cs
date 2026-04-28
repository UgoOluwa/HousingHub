using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Property;

namespace HousingHub.Service.PropertyService.Interfaces;

public interface IPropertyCommandService
{
    Task<BaseResponse<PropertyDto>> CreateProperty(CreatePropertyDto request, Guid authenticatedUserId);
    Task<BaseResponse<PropertyDto>> UpdateProperty(UpdatePropertyDto request, Guid authenticatedUserId);
    Task<BaseResponse<bool>> DeleteProperty(Guid propertyId, Guid authenticatedUserId);

    /// <summary>Admin: publish or unpublish a property listing.</summary>
    Task<BaseResponse<bool>> SetPropertyPublishedAsync(Guid propertyId, bool isPublished);

    /// <summary>Admin: delete any property without ownership check.</summary>
    Task<BaseResponse<bool>> AdminDeletePropertyAsync(Guid propertyId);
}
