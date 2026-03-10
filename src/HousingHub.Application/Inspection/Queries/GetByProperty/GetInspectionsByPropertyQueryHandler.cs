using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Queries.GetByProperty;

public class GetInspectionsByPropertyQueryHandler : IRequestHandler<GetInspectionsByPropertyQuery, BaseResponsePagination<PaginatedResult<InspectionDto>>>
{
    private readonly IInspectionQueryService _inspectionQueryService;

    public GetInspectionsByPropertyQueryHandler(IInspectionQueryService inspectionQueryService)
    {
        _inspectionQueryService = inspectionQueryService;
    }

    public async Task<BaseResponsePagination<PaginatedResult<InspectionDto>>> Handle(GetInspectionsByPropertyQuery request, CancellationToken cancellationToken)
    {
        var response = await _inspectionQueryService.GetInspectionsByPropertyAsync(request.PropertyId, request.PageNumber, request.PageSize, request.Status);
        var paginatedResponse = new BaseResponsePagination<PaginatedResult<InspectionDto>>(
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
