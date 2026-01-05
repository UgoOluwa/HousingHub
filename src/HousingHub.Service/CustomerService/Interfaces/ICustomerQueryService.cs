using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Service.CustomerService.Interfaces;

public interface ICustomerQueryService
{
    Task<BaseResponse<CustomerWithDetailsDto?>> GetCustomerAsync(Guid id);
    Task<BaseResponse<List<CustomerDto>>> GetAllCustomersAsync();
}
