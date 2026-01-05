using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.CustomerAddress;

namespace HousingHub.Service.CustomerAddressService.Interfaces;

public interface ICustomerAddressQueryService
{
    Task<BaseResponse<CustomerAddressDto?>> GetAddressAsync(Guid id);
    Task<BaseResponse<List<CustomerAddressDto>>> GetAllCustomerAddressesAsync();
    Task<BaseResponse<CustomerAddressDto?>> GetCustomerAddressByCustomerIdAsync(Guid customerId);
}
