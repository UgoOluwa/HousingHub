using HousingHub.Application.Commons.Bases;
using HousingHub.Service.InspectionService.Interfaces;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Cancel;

public class CancelInspectionCommandHandler : IRequestHandler<CancelInspectionCommand, BaseResponse<bool>>
{
    private readonly IInspectionCommandService _inspectionCommandService;

    public CancelInspectionCommandHandler(IInspectionCommandService inspectionCommandService)
    {
        _inspectionCommandService = inspectionCommandService;
    }

    public async Task<BaseResponse<bool>> Handle(CancelInspectionCommand request, CancellationToken cancellationToken)
    {
        var response = await _inspectionCommandService.CancelInspectionAsync(request.InspectionId, request.AuthenticatedUserId);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
