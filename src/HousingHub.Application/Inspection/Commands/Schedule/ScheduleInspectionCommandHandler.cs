using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Schedule;

public class ScheduleInspectionCommandHandler : IRequestHandler<ScheduleInspectionCommand, BaseResponse<InspectionDto?>>
{
    private readonly IInspectionCommandService _inspectionCommandService;

    public ScheduleInspectionCommandHandler(IInspectionCommandService inspectionCommandService)
    {
        _inspectionCommandService = inspectionCommandService;
    }

    public async Task<BaseResponse<InspectionDto?>> Handle(ScheduleInspectionCommand request, CancellationToken cancellationToken)
    {
        var dto = new ScheduleInspectionDto(request.PropertyId, request.ScheduledDate, request.ScheduledTime, request.Note);
        var response = await _inspectionCommandService.ScheduleInspectionAsync(dto, request.AuthenticatedUserId);
        return new BaseResponse<InspectionDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
