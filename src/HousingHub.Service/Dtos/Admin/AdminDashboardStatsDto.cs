namespace HousingHub.Service.Dtos.Admin;

public record AdminDashboardStatsDto(
    int TotalCustomers,
    int TotalOwners,
    int TotalAgents,
    int PendingKyc,
    int ActiveListings,
    int TotalProperties,
    int PendingInspections,
    int TodaysInspections);
