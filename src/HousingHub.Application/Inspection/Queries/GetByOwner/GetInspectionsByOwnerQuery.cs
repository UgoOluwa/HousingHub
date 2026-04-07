using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Queries.GetByOwner;

public record GetInspectionsByOwnerQuery(
    Guid OwnerId,
    int PageNumber = 1,
    int PageSize = 10,
    InspectionStatus? Status = null) : IRequest<BaseResponsePagination<PaginatedResult<OwnerInspectionDto>>>;
