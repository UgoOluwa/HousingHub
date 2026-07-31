using HousingHub.Service.Dtos.Admin;

namespace HousingHub.Service.AdminService;

public interface IAdminAuthService
{
    /// <summary>Generates and emails a one-time login code, if the email belongs to an active admin. Always succeeds outwardly to avoid revealing whether the email exists.</summary>
    Task RequestOtpAsync(string email);
    /// <summary>Verifies a one-time login code and, if valid and unexpired, issues a JWT and invalidates the code.</summary>
    Task<AdminLoginResultDto?> VerifyOtpAsync(string email, string code);
    Task CreateAdminAsync(string email, string password, string firstName, string lastName);
    /// <summary>Creates a new staff admin account with a system-generated password, since login is OTP-only — no seed key required, callable by any already-authenticated admin.</summary>
    Task CreateStaffAsync(string email, string firstName, string lastName);

    Task<AdminProfileDto?> GetAdminProfileAsync(Guid adminId);
    Task<bool> UpdateAdminProfileAsync(Guid adminId, UpdateAdminProfileDto dto);
    Task<bool> ChangeAdminPasswordAsync(Guid adminId, string currentPassword, string newPassword);
    Task<List<AdminStaffDto>> GetAllStaffAsync();
    Task<bool> DeactivateAdminAsync(Guid adminId);
    Task<bool> ReactivateAdminAsync(Guid adminId);
}
