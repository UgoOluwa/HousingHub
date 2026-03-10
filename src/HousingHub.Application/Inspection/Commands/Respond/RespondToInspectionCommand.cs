using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Respond;

public record RespondToInspectionCommand(
    Guid InspectionId,
    bool Accept,
    string? Note,
    Guid AuthenticatedUserId) : IRequest<BaseResponse<InspectionDto?>>;
