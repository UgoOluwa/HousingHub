using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Complete;

public class CompleteInspectionCommandHandler
    : IRequestHandler<CompleteInspectionCommand, BaseResponse<InspectionDto?>>
{
    private readonly IInspectionCommandService _inspectionCommandService;

    public CompleteInspectionCommandHandler(IInspectionCommandService inspectionCommandService)
    {
        _inspectionCommandService = inspectionCommandService;
    }

    public async Task<BaseResponse<InspectionDto?>> Handle(
        CompleteInspectionCommand request, CancellationToken cancellationToken)
    {
        var response = await _inspectionCommandService.CompleteInspectionAsync(
            request.InspectionId, request.Comment, request.AuthenticatedUserId);

        return new BaseResponse<InspectionDto?>(
            response.IsSuccessful, response.Data, response.Message, null);
    }
}
