using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Admin;

public record AdminInspectionListDto(
    Guid Id,
    string InspectionId,
    string PropertyName,
    string PropertyAddress,
    Guid PropertyId,
    Guid CustomerId,
    string CustomerName,
    DateTime ScheduledDate,
    TimeSpan ScheduledTime,
    DateTime DateRequested,
    InspectionStatus Status,
    string? Note,
    string? DeclineNote);
