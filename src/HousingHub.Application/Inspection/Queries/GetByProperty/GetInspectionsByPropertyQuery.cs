using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Queries.GetByProperty;

public record GetInspectionsByPropertyQuery(
    Guid PropertyId,
    int PageNumber = 1,
    int PageSize = 10,
    InspectionStatus? Status = null) : IRequest<BaseResponsePagination<PaginatedResult<InspectionDto>>>;
