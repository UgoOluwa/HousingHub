using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Admin;

public record AdminTodayInspectionDto(
    Guid Id,
    string InspectionId,
    string PropertyName,
    string PropertyAddress,
    string CustomerName,
    string CustomerPhone,
    DateTime ScheduledDate,
    TimeSpan ScheduledTime,
    DateTime DateRequested,
    InspectionStatus Status);
