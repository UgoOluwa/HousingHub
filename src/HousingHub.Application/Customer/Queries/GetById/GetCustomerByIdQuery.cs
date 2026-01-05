using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Customer.Queries.GetById;

public record GetCustomerByIdQuery(Guid Id) : IRequest<BaseResponse<CustomerWithDetailsDto>>;
