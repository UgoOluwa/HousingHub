using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Cancel;

public record CancelInspectionCommand(Guid InspectionId, Guid AuthenticatedUserId) : IRequest<BaseResponse<bool>>;
