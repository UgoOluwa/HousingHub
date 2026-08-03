using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Inspection;

public record InspectionDto(
    Guid Id,
    string InspectionId,
    DateTime DateCreated,
    DateTime DateModified,
    Guid CustomerId,
    Guid PropertyId,
    DateTime ScheduledDate,
    TimeSpan ScheduledTime,
    string? Note,
    InspectionStatus Status,
    string? DeclineNote,
    DateTime? RescheduledDate,
    TimeSpan? RescheduledTime,
    string? RescheduleNote,
    string? PropertyImageUrl = null,
    Guid? PropertyOwnerId = null,
    string? CustomerName = null,
    string? PropertyOwnerName = null,
    DateTime? HandedOffAt = null,
    Guid? AssignedStaffId = null,
    string? AssignedStaffName = null);
