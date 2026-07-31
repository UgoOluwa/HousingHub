using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Inspection;

// Field names/order intentionally mirror InspectionDto (Id + DateCreated, not
// InspectionId-only + DateRequested) so the frontend's single Inspection type
// works uniformly regardless of whether it came from the owner or customer endpoint.
public record OwnerInspectionDto(
    Guid Id,
    string InspectionId,
    string PropertyName,
    double? Latitude,
    double? Longitude,
    DateTime ScheduledDate,
    TimeSpan ScheduledTime,
    DateTime DateCreated,
    InspectionStatus Status,
    string? PropertyImageUrl = null);
