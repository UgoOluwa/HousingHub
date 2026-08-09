using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using HousingHub.Core.CustomResponses;
using HousingHub.Model.Entities;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HousingHub.Service.AdminService;

public class AdminAuthService(
    IDynamoDBContext dynamoDb,
    IPasswordHasher passwordHasher,
    IEmailService emailService,
    IConfiguration configuration) : IAdminAuthService
{
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan OtpResendCooldown = TimeSpan.FromSeconds(60);
    private const int MaxOtpAttempts = 5;
    private static readonly TimeSpan RefreshTokenValidity = TimeSpan.FromDays(30);

    public async Task<BaseResponse<bool>> RequestOtpAsync(string email)
    {
        var results = await dynamoDb.QueryAsync<Admin>(
            email,
            new DynamoDBOperationConfig { IndexName = "Email-index" })
            .GetRemainingAsync();

        var admin = results.FirstOrDefault(a => a.IsActive);

        // Every branch below returns the exact same response — whether the email
        // doesn't exist, was throttled, or really got a fresh code — so a caller can
        // never distinguish "not registered" from "already has a code outstanding."
        if (admin == null)
            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.OtpSent);

        if (admin.OtpRequestedAt.HasValue && DateTime.UtcNow - admin.OtpRequestedAt.Value < OtpResendCooldown)
            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.OtpSent);

        string code = Random.Shared.Next(0, 1_000_000).ToString("D6");
        admin.OtpCode = code;
        admin.OtpExpiresAt = DateTime.UtcNow.Add(OtpValidity);
        admin.OtpRequestedAt = DateTime.UtcNow;
        admin.OtpAttempts = 0;
        admin.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(admin);

        await emailService.SendAdminOtpAsync(admin.Email, admin.FirstName, code);

        return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.OtpSent);
    }

    public async Task<BaseResponse<AdminLoginResultDto>> VerifyOtpAsync(string email, string code)
    {
        var results = await dynamoDb.QueryAsync<Admin>(
            email,
            new DynamoDBOperationConfig { IndexName = "Email-index" })
            .GetRemainingAsync();

        var admin = results.FirstOrDefault(a => a.IsActive);
        if (admin == null)
            return new BaseResponse<AdminLoginResultDto>(null, false, string.Empty, ResponseMessages.OtpInvalidOrExpired);

        if (string.IsNullOrEmpty(admin.OtpCode)
            || admin.OtpExpiresAt == null
            || admin.OtpExpiresAt < DateTime.UtcNow)
            return new BaseResponse<AdminLoginResultDto>(null, false, string.Empty, ResponseMessages.OtpInvalidOrExpired);

        if (admin.OtpCode != code)
        {
            admin.OtpAttempts++;

            // Too many wrong guesses against this code — burn it so the attempt limit
            // can't be reset by just requesting a fresh code before it's exhausted.
            if (admin.OtpAttempts >= MaxOtpAttempts)
            {
                admin.OtpCode = null;
                admin.OtpExpiresAt = null;
                admin.OtpAttempts = 0;
                admin.DateModified = DateTime.UtcNow;
                await dynamoDb.SaveAsync(admin);
                return new BaseResponse<AdminLoginResultDto>(null, false, string.Empty, ResponseMessages.OtpTooManyAttempts);
            }

            admin.DateModified = DateTime.UtcNow;
            await dynamoDb.SaveAsync(admin);
            return new BaseResponse<AdminLoginResultDto>(null, false, string.Empty, ResponseMessages.OtpInvalidOrExpired);
        }

        // One-time use — invalidate immediately so it can't be replayed.
        admin.OtpCode = null;
        admin.OtpExpiresAt = null;
        admin.OtpRequestedAt = null;
        admin.OtpAttempts = 0;
        admin.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(admin);

        var token = CreateToken(admin);
        var refreshToken = await IssueRefreshTokenAsync(admin.Id);
        var dto = new AdminLoginResultDto(admin.Id, token, admin.FirstName, admin.LastName, admin.Email, refreshToken, admin.Role);
        return new BaseResponse<AdminLoginResultDto>(dto, true, string.Empty, ResponseMessages.LoginSuccess);
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token, rotating the refresh token
    /// in the process. Mirrors the Customer-side AuthService.RefreshToken logic:
    /// the presented token is revoked and a fresh one issued, and a token that's
    /// already been revoked (i.e. presented a second time — a replay) revokes every
    /// other active token for the same admin, since that can only mean it was stolen.
    /// </summary>
    public async Task<AdminLoginResultDto?> RefreshTokenAsync(string refreshToken)
    {
        string tokenHash = HashToken(refreshToken);

        var matches = await dynamoDb.QueryAsync<AdminRefreshToken>(
            tokenHash,
            new DynamoDBOperationConfig { IndexName = "TokenHash-index" })
            .GetRemainingAsync();

        var existing = matches.FirstOrDefault();
        if (existing == null) return null;

        if (existing.IsRevoked)
        {
            await RevokeAllRefreshTokensAsync(existing.AdminId);
            return null;
        }

        if (existing.ExpiresAt < DateTime.UtcNow) return null;

        var admin = await dynamoDb.LoadAsync<Admin>(existing.AdminId);
        if (admin == null || !admin.IsActive) return null;

        existing.IsRevoked = true;
        existing.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(existing);

        var newAccessToken = CreateToken(admin);
        var newRefreshToken = await IssueRefreshTokenAsync(admin.Id);

        return new AdminLoginResultDto(admin.Id, newAccessToken, admin.FirstName, admin.LastName, admin.Email, newRefreshToken, admin.Role);
    }

    private async Task<string> IssueRefreshTokenAsync(Guid adminId)
    {
        string rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var refreshToken = new AdminRefreshToken
        {
            Id = Guid.NewGuid(),
            AdminId = adminId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenValidity),
            IsRevoked = false,
            IsActive = true,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };

        await dynamoDb.SaveAsync(refreshToken);
        return rawToken;
    }

    /// <summary>
    /// Ends an admin session. Silent on unknown or already-revoked tokens: the session
    /// is over either way, and distinguishing the cases would confirm whether a token
    /// value is real.
    /// </summary>
    public async Task LogoutAsync(string refreshToken, bool allSessions = false)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var matches = await dynamoDb.QueryAsync<AdminRefreshToken>(
            HashToken(refreshToken),
            new DynamoDBOperationConfig { IndexName = "TokenHash-index" })
            .GetRemainingAsync();

        var existing = matches.FirstOrDefault();
        if (existing is null) return;

        if (allSessions)
        {
            await RevokeAllRefreshTokensAsync(existing.AdminId);
            return;
        }

        if (existing.IsRevoked) return;

        existing.IsRevoked = true;
        existing.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(existing);
    }

    private async Task RevokeAllRefreshTokensAsync(Guid adminId)
    {
        var tokens = await dynamoDb.QueryAsync<AdminRefreshToken>(
            adminId,
            new DynamoDBOperationConfig { IndexName = "AdminId-index" })
            .GetRemainingAsync();

        foreach (var refreshToken in tokens.Where(t => !t.IsRevoked))
        {
            refreshToken.IsRevoked = true;
            refreshToken.DateModified = DateTime.UtcNow;
            await dynamoDb.SaveAsync(refreshToken);
        }
    }

    private static string HashToken(string rawToken)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }

    public async Task CreateStaffAsync(string email, string firstName, string lastName, string role)
    {
        // Login is OTP-only, so this password is never used to authenticate —
        // generate a throwaway value purely to satisfy Admin.PasswordHash's
        // non-null constraint.
        string throwawayPassword = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        await CreateAdminAsync(email, throwawayPassword, firstName, lastName, role);
    }

    public Task CreateAdminAsync(string email, string password, string firstName, string lastName) =>
        CreateAdminAsync(email, password, firstName, lastName, AdminRoles.Admin);

    private async Task CreateAdminAsync(string email, string password, string firstName, string lastName, string role)
    {
        var admin = new Admin
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.Hash(password),
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            IsActive = true,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };

        await dynamoDb.SaveAsync(admin);
    }

    public async Task<AdminProfileDto?> GetAdminProfileAsync(Guid adminId)
    {
        var admin = await dynamoDb.LoadAsync<Admin>(adminId);
        if (admin == null) return null;

        return new AdminProfileDto(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.DateCreated, admin.IsActive, admin.Role);
    }

    public async Task<bool> PromoteToSuperAdminAsync(string email)
    {
        var results = await dynamoDb.QueryAsync<Admin>(
            email,
            new DynamoDBOperationConfig { IndexName = "Email-index" })
            .GetRemainingAsync();

        var admin = results.FirstOrDefault();
        if (admin == null) return false;

        admin.Role = AdminRoles.SuperAdmin;
        admin.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(admin);
        return true;
    }

    public async Task<bool> UpdateAdminProfileAsync(Guid adminId, UpdateAdminProfileDto dto)
    {
        var admin = await dynamoDb.LoadAsync<Admin>(adminId);
        if (admin == null) return false;

        if (!string.IsNullOrWhiteSpace(dto.FirstName)) admin.FirstName = dto.FirstName;
        if (!string.IsNullOrWhiteSpace(dto.LastName)) admin.LastName = dto.LastName;
        admin.DateModified = DateTime.UtcNow;

        await dynamoDb.SaveAsync(admin);
        return true;
    }

    public async Task<bool> ChangeAdminPasswordAsync(Guid adminId, string currentPassword, string newPassword)
    {
        var admin = await dynamoDb.LoadAsync<Admin>(adminId);
        if (admin == null) return false;

        if (!passwordHasher.Verify(currentPassword, admin.PasswordHash)) return false;

        admin.PasswordHash = passwordHasher.Hash(newPassword);
        admin.DateModified = DateTime.UtcNow;

        await dynamoDb.SaveAsync(admin);
        return true;
    }

    public async Task<List<AdminStaffDto>> GetAllStaffAsync()
    {
        var scan = dynamoDb.ScanAsync<Admin>(new List<ScanCondition>());
        var admins = await scan.GetRemainingAsync();

        return admins
            .OrderByDescending(a => a.DateCreated)
            .Select(a => new AdminStaffDto(a.Id, a.FirstName, a.LastName, a.Email, a.DateCreated, a.IsActive, a.Role))
            .ToList();
    }

    public async Task<bool> DeactivateAdminAsync(Guid adminId)
    {
        var admin = await dynamoDb.LoadAsync<Admin>(adminId);
        if (admin == null) return false;

        admin.IsActive = false;
        admin.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(admin);

        // Deactivation previously only flipped a flag. RefreshTokenAsync does check
        // IsActive, but the already-issued access token stayed valid for its full
        // lifetime, and the refresh token was never revoked. Revoke the family so the
        // deactivated admin cannot mint another access token.
        await RevokeAllRefreshTokensAsync(adminId);

        return true;
    }

    public async Task<bool> ReactivateAdminAsync(Guid adminId)
    {
        var admin = await dynamoDb.LoadAsync<Admin>(adminId);
        if (admin == null) return false;

        admin.IsActive = true;
        admin.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(admin);
        return true;
    }

    private string CreateToken(Admin admin)
    {
        string secretKey = configuration["AdminJwt:Secret"]!;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Fall back to 30 minutes, matching appsettings, rather than the previous 8
        // hours. Admin tokens are not revocable once issued, so a misconfigured
        // environment should shorten the blast radius, not widen it.
        int expirationInMinutes = int.TryParse(configuration["AdminJwt:ExpirationInMinutes"], out var mins) ? mins : 30;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, admin.Email),
                new Claim(JwtRegisteredClaimNames.GivenName, admin.FirstName),
                new Claim(JwtRegisteredClaimNames.FamilyName, admin.LastName),
                new Claim("role", "Admin"),
                new Claim("adminRole", admin.Role)
            ]),
            Expires = DateTime.UtcNow.AddMinutes(expirationInMinutes),
            SigningCredentials = credentials,
            Issuer = configuration["AdminJwt:Issuer"],
            Audience = configuration["AdminJwt:Audience"]
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }
}
