namespace HousingHub.Service.Dtos.Inspection;

public record ScheduleInspectionDto(
    Guid PropertyId,
    DateTime ScheduledDate,
    TimeSpan ScheduledTime,
    string? Note);
