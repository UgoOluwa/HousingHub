using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Property;

namespace HousingHub.Service.PropertyService.Interfaces;

public interface IPropertyCommandService
{
    Task<BaseResponse<PropertyDto>> CreateProperty(CreatePropertyDto request, Guid authenticatedUserId);
    Task<BaseResponse<PropertyDto>> UpdateProperty(UpdatePropertyDto request, Guid authenticatedUserId);
    Task<BaseResponse<bool>> DeleteProperty(Guid propertyId, Guid authenticatedUserId);

    /// <summary>Admin: publish or unpublish a property listing, bypassing ownership checks.
    /// When unpublishing, <paramref name="reason"/> is persisted and emailed to the owner.</summary>
    Task<BaseResponse<bool>> SetPropertyPublishedAsync(Guid propertyId, bool isPublished, string? reason = null);

    /// <summary>Owner: publish or unpublish their own saved/draft property listing.</summary>
    Task<BaseResponse<bool>> SetPropertyPublishedAsync(Guid propertyId, bool isPublished, Guid authenticatedUserId);

    /// <summary>Admin: delete any property without ownership check. The owner is emailed with <paramref name="reason"/>.</summary>
    Task<BaseResponse<bool>> AdminDeletePropertyAsync(Guid propertyId, string reason);

    /// <summary>Admin: mark a property as verified or unverified.</summary>
    Task<BaseResponse<bool>> SetPropertyVerifiedAsync(Guid propertyId, bool isVerified);
}
