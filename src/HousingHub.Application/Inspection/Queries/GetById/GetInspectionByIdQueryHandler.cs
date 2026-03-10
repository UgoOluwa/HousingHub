using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Queries.GetById;

public class GetInspectionByIdQueryHandler : IRequestHandler<GetInspectionByIdQuery, BaseResponse<InspectionDto?>>
{
    private readonly IInspectionQueryService _inspectionQueryService;

    public GetInspectionByIdQueryHandler(IInspectionQueryService inspectionQueryService)
    {
        _inspectionQueryService = inspectionQueryService;
    }

    public async Task<BaseResponse<InspectionDto?>> Handle(GetInspectionByIdQuery request, CancellationToken cancellationToken)
    {
        var response = await _inspectionQueryService.GetInspectionAsync(request.Id);
        return new BaseResponse<InspectionDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
