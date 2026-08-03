using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.HandOff;

public class HandOffInspectionCommandHandler : IRequestHandler<HandOffInspectionCommand, BaseResponse<InspectionDto?>>
{
    private readonly IInspectionCommandService _inspectionCommandService;

    public HandOffInspectionCommandHandler(IInspectionCommandService inspectionCommandService)
    {
        _inspectionCommandService = inspectionCommandService;
    }

    public async Task<BaseResponse<InspectionDto?>> Handle(HandOffInspectionCommand request, CancellationToken cancellationToken)
    {
        var response = await _inspectionCommandService.HandOffToHousingHubAsync(request.InspectionId, request.AuthenticatedUserId);
        return new BaseResponse<InspectionDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
