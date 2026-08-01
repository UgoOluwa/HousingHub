using HousingHub.Service.AdminService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminAuthController(IAdminAuthService adminAuthService) : ControllerBase
{
    /// <summary>Requests a one-time login code by email. Always responds the same way regardless of whether the email is registered, to avoid account enumeration.</summary>
    [AllowAnonymous]
    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp([FromBody] AdminOtpRequest request)
    {
        await adminAuthService.RequestOtpAsync(request.Email);
        return Ok(new { message = "If that email is registered, a login code has been sent." });
    }

    /// <summary>Verifies a one-time login code and, on success, issues a JWT.</summary>
    [AllowAnonymous]
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] AdminOtpVerifyRequest request)
    {
        var result = await adminAuthService.VerifyOtpAsync(request.Email, request.Code);
        if (result == null) return Unauthorized(new { message = "Invalid or expired code" });
        return Ok(result);
    }

    /// <summary>Exchanges a refresh token for a new access token and a rotated refresh token.</summary>
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] AdminRefreshTokenRequest request)
    {
        var result = await adminAuthService.RefreshTokenAsync(request.RefreshToken);
        if (result == null) return Unauthorized(new { message = "Invalid or expired refresh token" });
        return Ok(result);
    }

    // Seeding endpoint — restrict in production via env var or remove after first use
    [AllowAnonymous]
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateAdminRequest request)
    {
        string? seedKey = Environment.GetEnvironmentVariable("ADMIN_SEED_KEY");
        if (string.IsNullOrEmpty(seedKey) || request.SeedKey != seedKey)
            return Forbid();

        await adminAuthService.CreateAdminAsync(request.Email, request.Password, request.FirstName, request.LastName);
        return Ok(new { message = "Admin created" });
    }
}

public record AdminOtpRequest(string Email);
public record AdminOtpVerifyRequest(string Email, string Code);
public record AdminRefreshTokenRequest(string RefreshToken);
public record CreateAdminRequest(string SeedKey, string Email, string Password, string FirstName, string LastName);
