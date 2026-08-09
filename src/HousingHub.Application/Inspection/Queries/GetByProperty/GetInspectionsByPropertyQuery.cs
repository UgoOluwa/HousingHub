using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Queries.GetByProperty;

/// <summary>
/// Inspections booked against a property. Owner-only — the results carry the
/// booking customer's id, visit times and free-text notes.
/// </summary>
/// <param name="RequestingUserId">
/// Set server-side from the caller's token, never from the request body.
/// </param>
public record GetInspectionsByPropertyQuery(
    Guid PropertyId,
    Guid RequestingUserId,
    int PageNumber = 1,
    int PageSize = 10,
    InspectionStatus? Status = null) : IRequest<BaseResponsePagination<PaginatedResult<InspectionDto>>>;
