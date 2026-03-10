using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.RespondReschedule;

public class RespondToRescheduleCommandHandler : IRequestHandler<RespondToRescheduleCommand, BaseResponse<InspectionDto?>>
{
    private readonly IInspectionCommandService _inspectionCommandService;

    public RespondToRescheduleCommandHandler(IInspectionCommandService inspectionCommandService)
    {
        _inspectionCommandService = inspectionCommandService;
    }

    public async Task<BaseResponse<InspectionDto?>> Handle(RespondToRescheduleCommand request, CancellationToken cancellationToken)
    {
        var response = await _inspectionCommandService.RespondToRescheduleAsync(request.InspectionId, request.Accept, request.AuthenticatedUserId);
        return new BaseResponse<InspectionDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
