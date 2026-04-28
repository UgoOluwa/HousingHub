using HousingHub.Service.Dtos.Admin;

namespace HousingHub.Service.AdminService;

public interface IAdminAuthService
{
    Task<AdminLoginResultDto?> LoginAsync(string email, string password);
    Task CreateAdminAsync(string email, string password, string firstName, string lastName);

    Task<AdminProfileDto?> GetAdminProfileAsync(Guid adminId);
    Task<bool> UpdateAdminProfileAsync(Guid adminId, UpdateAdminProfileDto dto);
    Task<bool> ChangeAdminPasswordAsync(Guid adminId, string currentPassword, string newPassword);
    Task<List<AdminStaffDto>> GetAllStaffAsync();
    Task<bool> DeactivateAdminAsync(Guid adminId);
    Task<bool> ReactivateAdminAsync(Guid adminId);
}
