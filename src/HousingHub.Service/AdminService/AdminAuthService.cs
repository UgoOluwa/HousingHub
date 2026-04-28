using System.Security.Claims;
using System.Text;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using HousingHub.Model.Entities;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Dtos.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HousingHub.Service.AdminService;

public class AdminAuthService(
    IDynamoDBContext dynamoDb,
    IPasswordHasher passwordHasher,
    IConfiguration configuration) : IAdminAuthService
{
    public async Task<AdminLoginResultDto?> LoginAsync(string email, string password)
    {
        var results = await dynamoDb.QueryAsync<Admin>(
            email,
            new DynamoDBOperationConfig { IndexName = "Email-index" })
            .GetRemainingAsync();

        var admin = results.FirstOrDefault(a => a.IsActive);
        if (admin == null) return null;

        if (!passwordHasher.Verify(password, admin.PasswordHash)) return null;

        var token = CreateToken(admin);
        return new AdminLoginResultDto(token, admin.FirstName, admin.LastName, admin.Email);
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
