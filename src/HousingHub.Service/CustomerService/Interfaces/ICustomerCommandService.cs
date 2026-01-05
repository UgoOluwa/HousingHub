using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Service.CustomerService.Interfaces;

public interface ICustomerCommandService
{
    Task<BaseResponse<CustomerDto>> CreateCustomer(CreateCustomerDto request);
    Task<BaseResponse<CustomerDto>> UpdateCustomer(UpdateCustomerDto request);
    Task<BaseResponse<bool>> DeleteCustomer(Guid customerId);
}
