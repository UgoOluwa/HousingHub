using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Queries.GetByCustomer;

public record GetInspectionsByCustomerQuery(
    Guid CustomerId,
    int PageNumber = 1,
    int PageSize = 10,
    InspectionStatus? Status = null) : IRequest<BaseResponsePagination<PaginatedResult<InspectionDto>>>;
