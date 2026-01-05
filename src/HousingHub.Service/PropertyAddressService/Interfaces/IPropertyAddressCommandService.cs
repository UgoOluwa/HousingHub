using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyAddress;

namespace HousingHub.Service.PropertyAddressService.Interfaces;

public interface IPropertyAddressCommandService
{
    Task<BaseResponse<PropertyAddressDto>> CreatePropertyAddress(CreatePropertyAddressDto request);
}
