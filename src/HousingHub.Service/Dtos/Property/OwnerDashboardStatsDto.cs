namespace HousingHub.Service.Dtos.Property;

public record OwnerDashboardStatsDto(
    int TotalProperties,
    int ActiveListings,
    int PendingInspections,
    int CompletedInspections);
