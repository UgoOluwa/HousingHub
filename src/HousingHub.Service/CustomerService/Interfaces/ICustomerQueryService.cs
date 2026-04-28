using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Service.CustomerService.Interfaces;

public interface ICustomerQueryService
{
    Task<BaseResponse<CustomerWithDetailsDto?>> GetCustomerAsync(Guid id);
    Task<BaseResponse<List<CustomerDto>>> GetAllCustomersAsync();
    Task<BaseResponse<PaginatedResult<CustomerDto>>> GetAllCustomersPaginatedAsync(int pageNumber, int pageSize);

    /// <summary>Admin: filtered paginated list, optionally scoped to a CustomerType flag.</summary>
    Task<BaseResponse<PaginatedResult<AdminCustomerListDto>>> GetCustomersFilteredAsync(
        AdminCustomerFilterDto filter, CustomerType? typeFlag = null);
}
