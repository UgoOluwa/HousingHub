using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyAddress;

namespace HousingHub.Service.PropertyAddressService.Interfaces;

public interface IPropertyAddressQueryService
{
    Task<BaseResponse<PropertyAddressDto?>> GetPropertyAddressAsync(Guid id);
    Task<BaseResponse<PropertyAddressDto?>> GetPropertyAddressByPropertyIdAsync(Guid propertyId);

}
