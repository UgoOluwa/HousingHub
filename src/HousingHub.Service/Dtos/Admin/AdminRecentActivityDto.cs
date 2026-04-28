namespace HousingHub.Service.Dtos.Admin;

public record AdminRecentActivityDto(
    string Type,
    string Description,
    DateTime OccurredAt,
    Guid? RelatedId);
