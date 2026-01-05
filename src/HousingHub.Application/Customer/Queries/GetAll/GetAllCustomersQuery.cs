using MediatR;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Application.Customer.Queries.GetAll;

public record GetAllCustomersQuery() : IRequest<BaseResponse<List<CustomerDto>>>;
