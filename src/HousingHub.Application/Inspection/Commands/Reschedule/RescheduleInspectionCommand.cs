using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Reschedule;

public record RescheduleInspectionCommand(
    Guid InspectionId,
    DateTime RescheduledDate,
    TimeSpan RescheduledTime,
    string? Note,
    Guid AuthenticatedUserId) : IRequest<BaseResponse<InspectionDto?>>;
