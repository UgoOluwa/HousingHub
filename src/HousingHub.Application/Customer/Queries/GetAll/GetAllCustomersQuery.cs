using MediatR;
using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Application.Customer.Queries.GetAll;

public record GetAllCustomersQuery(int PageNumber = 1, int PageSize = 10) : IRequest<BaseResponsePagination<PaginatedResult<CustomerDto>>>;
