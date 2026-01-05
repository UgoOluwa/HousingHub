using HousingHub.Application.Commons.Bases;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Customer.Queries.GetById;

public class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, BaseResponse<CustomerWithDetailsDto>>
{
    public readonly ICustomerQueryService _customerQueryService;

    public GetCustomerByIdQueryHandler(ICustomerQueryService customerQueryService)
    {
        _customerQueryService = customerQueryService;
    }

    public async Task<BaseResponse<CustomerWithDetailsDto>> Handle(GetCustomerByIdQuery query, CancellationToken cancellationToken)
    {
        var response = await _customerQueryService.GetCustomerAsync(query.Id);
        return new BaseResponse<CustomerWithDetailsDto>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
