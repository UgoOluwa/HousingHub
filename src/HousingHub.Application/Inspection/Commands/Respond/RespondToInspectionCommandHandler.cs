using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Respond;

public class RespondToInspectionCommandHandler : IRequestHandler<RespondToInspectionCommand, BaseResponse<InspectionDto?>>
{
    private readonly IInspectionCommandService _inspectionCommandService;

    public RespondToInspectionCommandHandler(IInspectionCommandService inspectionCommandService)
    {
        _inspectionCommandService = inspectionCommandService;
    }

    public async Task<BaseResponse<InspectionDto?>> Handle(RespondToInspectionCommand request, CancellationToken cancellationToken)
    {
        var dto = new RespondToInspectionDto(request.InspectionId, request.Accept, request.Note);
        var response = await _inspectionCommandService.RespondToInspectionAsync(dto, request.AuthenticatedUserId);
        return new BaseResponse<InspectionDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
