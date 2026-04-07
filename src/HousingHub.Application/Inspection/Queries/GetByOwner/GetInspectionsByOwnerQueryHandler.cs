using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Queries.GetByOwner;

public class GetInspectionsByOwnerQueryHandler : IRequestHandler<GetInspectionsByOwnerQuery, BaseResponsePagination<PaginatedResult<OwnerInspectionDto>>>
{
    private readonly IInspectionQueryService _inspectionQueryService;

    public GetInspectionsByOwnerQueryHandler(IInspectionQueryService inspectionQueryService)
    {
        _inspectionQueryService = inspectionQueryService;
    }

    public async Task<BaseResponsePagination<PaginatedResult<OwnerInspectionDto>>> Handle(GetInspectionsByOwnerQuery request, CancellationToken cancellationToken)
    {
        var response = await _inspectionQueryService.GetInspectionsByOwnerAsync(
            request.OwnerId, request.PageNumber, request.PageSize, request.Status);
        var paginatedResponse = new BaseResponsePagination<PaginatedResult<OwnerInspectionDto>>(
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
