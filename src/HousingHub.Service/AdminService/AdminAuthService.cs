using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
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
    private static readonly TimeSpan RefreshTokenValidity = TimeSpan.FromDays(30);

    public async Task RequestOtpAsync(string email)
    {
        var results = await dynamoDb.QueryAsync<Admin>(
            email,
            new DynamoDBOperationConfig { IndexName = "Email-index" })
            .GetRemainingAsync();

        var admin = results.FirstOrDefault(a => a.IsActive);
        if (admin == null) return; // don't reveal whether the email exists

        string code = Random.Shared.Next(0, 1_000_000).ToString("D6");
        admin.OtpCode = code;
        admin.OtpExpiresAt = DateTime.UtcNow.Add(OtpValidity);
        admin.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(admin);

        await emailService.SendAdminOtpAsync(admin.Email, admin.FirstName, code);
    }

    public async Task<AdminLoginResultDto?> VerifyOtpAsync(string email, string code)
    {
        var results = await dynamoDb.QueryAsync<Admin>(
            email,
            new DynamoDBOperationConfig { IndexName = "Email-index" })
            .GetRemainingAsync();

        var admin = results.FirstOrDefault(a => a.IsActive);
        if (admin == null) return null;

        if (string.IsNullOrEmpty(admin.OtpCode)
            || admin.OtpExpiresAt == null
            || admin.OtpExpiresAt < DateTime.UtcNow
            || admin.OtpCode != code)
            return null;

        // One-time use — invalidate immediately so it can't be replayed.
        admin.OtpCode = null;
        admin.OtpExpiresAt = null;
        admin.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(admin);

        var token = CreateToken(admin);
        var refreshToken = await IssueRefreshTokenAsync(admin.Id);
        return new AdminLoginResultDto(token, admin.FirstName, admin.LastName, admin.Email, refreshToken);
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

        return new AdminLoginResultDto(newAccessToken, admin.FirstName, admin.LastName, admin.Email, newRefreshToken);
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

    public async Task CreateStaffAsync(string email, string firstName, string lastName)
    {
        // Login is OTP-only, so this password is never used to authenticate —
        // generate a throwaway value purely to satisfy Admin.PasswordHash's
        // non-null constraint.
        string throwawayPassword = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        await CreateAdminAsync(email, throwawayPassword, firstName, lastName);
    }

    public async Task CreateAdminAsync(string email, string password, string firstName, string lastName)
    {
        var admin = new Admin
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.Hash(password),
            FirstName = firstName,
            LastName = lastName,
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

        return new AdminProfileDto(admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.DateCreated, admin.IsActive);
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
            .Select(a => new AdminStaffDto(a.Id, a.FirstName, a.LastName, a.Email, a.DateCreated, a.IsActive))
            .ToList();
    }

    public async Task<bool> DeactivateAdminAsync(Guid adminId)
    {
        var admin = await dynamoDb.LoadAsync<Admin>(adminId);
        if (admin == null) return false;

        admin.IsActive = false;
        admin.DateModified = DateTime.UtcNow;
        await dynamoDb.SaveAsync(admin);
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

        int expirationInMinutes = int.TryParse(configuration["AdminJwt:ExpirationInMinutes"], out var mins) ? mins : 480;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, admin.Email),
                new Claim(JwtRegisteredClaimNames.GivenName, admin.FirstName),
                new Claim(JwtRegisteredClaimNames.FamilyName, admin.LastName),
                new Claim("role", "Admin")
            ]),
            Expires = DateTime.UtcNow.AddMinutes(expirationInMinutes),
            SigningCredentials = credentials,
            Issuer = configuration["AdminJwt:Issuer"],
            Audience = configuration["AdminJwt:Audience"]
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }
}
