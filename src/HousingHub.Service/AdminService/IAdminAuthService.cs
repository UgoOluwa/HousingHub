using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Admin;

namespace HousingHub.Service.AdminService;

public interface IAdminAuthService
{
    /// <summary>
    /// Generates and emails a one-time login code, if the email belongs to an active
    /// admin who isn't already within the resend cooldown. Always succeeds outwardly
    /// (same response either way) to avoid revealing whether the email exists.
    /// </summary>
    Task<BaseResponse<bool>> RequestOtpAsync(string email);
    /// <summary>Verifies a one-time login code and, if valid and unexpired, issues a JWT and invalidates the code. Locks out the code after too many wrong attempts.</summary>
    Task<BaseResponse<AdminLoginResultDto>> VerifyOtpAsync(string email, string code);
    /// <summary>Exchanges a valid, unexpired refresh token for a new access token and a rotated refresh token.</summary>
    Task<AdminLoginResultDto?> RefreshTokenAsync(string refreshToken);
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
