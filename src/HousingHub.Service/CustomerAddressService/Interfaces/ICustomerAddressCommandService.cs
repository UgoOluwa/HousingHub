using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.CustomerAddress;

namespace HousingHub.Service.CustomerAddressService.Interfaces;

public interface ICustomerAddressCommandService
{
    Task<BaseResponse<CustomerAddressDto>> CreateCustomerAddress(CreateCustomerAddressDto request);
}
