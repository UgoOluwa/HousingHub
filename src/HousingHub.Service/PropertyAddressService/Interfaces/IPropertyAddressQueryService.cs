using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyAddress;

namespace HousingHub.Service.PropertyAddressService.Interfaces;

public interface IPropertyAddressQueryService
{
    /// <param name="requestingUserId">Caller id, or null when anonymous.</param>
    /// <param name="includeUnpublished">
    /// Administrative override. The admin API moderates draft and unpublished
    /// listings, so it must see their addresses; consumer callers must never set this.
    /// </param>
    Task<BaseResponse<PropertyAddressDto?>> GetPropertyAddressAsync(Guid id, Guid? requestingUserId = null, bool includeUnpublished = false);

    /// <inheritdoc cref="GetPropertyAddressAsync" />
    Task<BaseResponse<PropertyAddressDto?>> GetPropertyAddressByPropertyIdAsync(Guid propertyId, Guid? requestingUserId = null, bool includeUnpublished = false);
}
