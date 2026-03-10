using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.RespondReschedule;

public record RespondToRescheduleCommand(
    Guid InspectionId,
    bool Accept,
    Guid AuthenticatedUserId) : IRequest<BaseResponse<InspectionDto?>>;
