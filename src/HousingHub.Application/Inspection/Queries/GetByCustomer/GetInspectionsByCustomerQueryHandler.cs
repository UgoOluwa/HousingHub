using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Queries.GetByCustomer;

public class GetInspectionsByCustomerQueryHandler : IRequestHandler<GetInspectionsByCustomerQuery, BaseResponsePagination<PaginatedResult<InspectionDto>>>
{
    private readonly IInspectionQueryService _inspectionQueryService;

    public GetInspectionsByCustomerQueryHandler(IInspectionQueryService inspectionQueryService)
    {
        _inspectionQueryService = inspectionQueryService;
    }

    public async Task<BaseResponsePagination<PaginatedResult<InspectionDto>>> Handle(GetInspectionsByCustomerQuery request, CancellationToken cancellationToken)
    {
        var response = await _inspectionQueryService.GetInspectionsByCustomerAsync(request.CustomerId, request.PageNumber, request.PageSize, request.Status);
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
