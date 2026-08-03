using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Property;

namespace HousingHub.Service.PropertyService.Interfaces;

public interface IPropertyCommandService
{
    /// <param name="onBehalfOfOwnerId">
    /// Set by an admin creating a listing on behalf of a HousingHub-managed owner —
    /// the target owner must exist, be a HouseOwner/Agent, and have
    /// <see cref="Model.Entities.Customer.IsManagedByHousingHub"/> set. When null
    /// (the normal self-service path), <paramref name="authenticatedUserId"/> is the owner.
    /// </param>
    Task<BaseResponse<CreatePropertyResultDto>> CreateProperty(CreatePropertyDto request, Guid authenticatedUserId, Guid? onBehalfOfOwnerId = null);

    /// <summary>Admin: clears a property's possible-duplicate flag after review (the listing is legitimate, not a duplicate).</summary>
    Task<BaseResponse<bool>> DismissDuplicateFlagAsync(Guid propertyId);
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
