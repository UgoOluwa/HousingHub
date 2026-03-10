using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Schedule;

public record ScheduleInspectionCommand(
    Guid PropertyId,
    DateTime ScheduledDate,
    TimeSpan ScheduledTime,
    string? Note,
    Guid AuthenticatedUserId) : IRequest<BaseResponse<InspectionDto?>>;
