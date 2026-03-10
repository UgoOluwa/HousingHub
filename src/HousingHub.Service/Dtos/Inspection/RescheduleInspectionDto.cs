namespace HousingHub.Service.Dtos.Inspection;

public record RescheduleInspectionDto(
    Guid InspectionId,
    DateTime RescheduledDate,
    TimeSpan RescheduledTime,
    string? Note);
