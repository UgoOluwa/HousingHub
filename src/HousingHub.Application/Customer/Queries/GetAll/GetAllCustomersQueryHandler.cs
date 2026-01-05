using MediatR;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.CustomerService.Interfaces;

namespace HousingHub.Application.Customer.Queries.GetAll;

public class GetAllCustomersQueryHandler : IRequestHandler<GetAllCustomersQuery, BaseResponse<List<CustomerDto>>>
{
    public readonly ICustomerQueryService _customerQueryService;

    public GetAllCustomersQueryHandler(ICustomerQueryService customerQueryService)
    {
        _customerQueryService = customerQueryService;
    }

    public async Task<BaseResponse<List<CustomerDto>>> Handle(GetAllCustomersQuery query, CancellationToken cancellationToken)
    {
        var response = await _customerQueryService.GetAllCustomersAsync();
        return new BaseResponse<List<CustomerDto>>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
