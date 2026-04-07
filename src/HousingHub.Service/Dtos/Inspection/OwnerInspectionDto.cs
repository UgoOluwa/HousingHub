using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Inspection;

public record OwnerInspectionDto(
    string InspectionId,
    string PropertyName,
    double? Latitude,
    double? Longitude,
    DateTime ScheduledDate,
    TimeSpan ScheduledTime,
    DateTime DateRequested,
    InspectionStatus Status);
