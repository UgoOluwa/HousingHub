using MediatR;
using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.CustomerService.Interfaces;

namespace HousingHub.Application.Customer.Queries.GetAll;

public class GetAllCustomersQueryHandler : IRequestHandler<GetAllCustomersQuery, BaseResponsePagination<PaginatedResult<CustomerDto>>>
{
    public readonly ICustomerQueryService _customerQueryService;

    public GetAllCustomersQueryHandler(ICustomerQueryService customerQueryService)
    {
        _customerQueryService = customerQueryService;
    }

    public async Task<BaseResponsePagination<PaginatedResult<CustomerDto>>> Handle(GetAllCustomersQuery query, CancellationToken cancellationToken)
    {
        var response = await _customerQueryService.GetAllCustomersPaginatedAsync(query.PageNumber, query.PageSize);
        var paginatedResponse = new BaseResponsePagination<PaginatedResult<CustomerDto>>(
            response.IsSuccessful, response.Data, response.Message, null);

        if (response.Data != null)
        {
            paginatedResponse.PageNumber = response.Data.PageNumber;
            paginatedResponse.TotalPages = response.Data.TotalPages;
            paginatedResponse.TotalCount = response.Data.TotalCount;
        }

        return paginatedResponse;
    }
}
