using HousingHub.Service.Dtos.Admin;

namespace HousingHub.Service.AdminService;

public interface IAdminDashboardService
{
    Task<AdminDashboardStatsDto> GetStatsAsync();
}
