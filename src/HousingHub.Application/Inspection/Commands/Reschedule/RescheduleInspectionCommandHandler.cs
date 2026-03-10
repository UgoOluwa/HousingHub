using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Reschedule;

public class RescheduleInspectionCommandHandler : IRequestHandler<RescheduleInspectionCommand, BaseResponse<InspectionDto?>>
{
    private readonly IInspectionCommandService _inspectionCommandService;

    public RescheduleInspectionCommandHandler(IInspectionCommandService inspectionCommandService)
    {
        _inspectionCommandService = inspectionCommandService;
    }

    public async Task<BaseResponse<InspectionDto?>> Handle(RescheduleInspectionCommand request, CancellationToken cancellationToken)
    {
        var dto = new RescheduleInspectionDto(request.InspectionId, request.RescheduledDate, request.RescheduledTime, request.Note);
        var response = await _inspectionCommandService.RescheduleInspectionAsync(dto, request.AuthenticatedUserId);
        return new BaseResponse<InspectionDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
