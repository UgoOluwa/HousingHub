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

    /// <summary>
    /// Revokes the presented refresh token, ending that admin session server-side.
    /// </summary>
    /// <param name="allSessions">Revoke every active token for the admin, not just this one.</param>
    Task LogoutAsync(string refreshToken, bool allSessions = false);
    Task CreateAdminAsync(string email, string password, string firstName, string lastName);
    /// <summary>Creates a new staff admin account with a system-generated password, since login is OTP-only. Restricted to SuperAdmins.</summary>
    Task CreateStaffAsync(string email, string firstName, string lastName, string role);

    Task<AdminProfileDto?> GetAdminProfileAsync(Guid adminId);
    Task<bool> UpdateAdminProfileAsync(Guid adminId, UpdateAdminProfileDto dto);
    Task<bool> ChangeAdminPasswordAsync(Guid adminId, string currentPassword, string newPassword);
    Task<List<AdminStaffDto>> GetAllStaffAsync();
    Task<bool> DeactivateAdminAsync(Guid adminId);
    Task<bool> ReactivateAdminAsync(Guid adminId);
    /// <summary>Promotes an existing admin (by email) to SuperAdmin. Secret-gated bootstrap operation — see InternalController.</summary>
    Task<bool> PromoteToSuperAdminAsync(string email);
}
