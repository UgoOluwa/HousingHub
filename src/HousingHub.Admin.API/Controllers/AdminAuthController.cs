using HousingHub.Core.CustomResponses;
using HousingHub.Service.AdminService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminAuthController(IAdminAuthService adminAuthService) : ControllerBase
{
    /// <summary>
    /// Requests a one-time login code by email. Always responds the same way
    /// regardless of whether the email is registered or was throttled, to avoid
    /// account enumeration — the resend cooldown is enforced server-side and is
    /// a fixed, publicly-known duration, so the client can run its own countdown
    /// without the response needing to reveal anything account-specific.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp([FromBody] AdminOtpRequest request)
    {
        var result = await adminAuthService.RequestOtpAsync(request.Email);
        return Ok(new { message = result.Message });
    }

    /// <summary>Verifies a one-time login code and, on success, issues a JWT. Locks out the code after too many wrong attempts.</summary>
    [AllowAnonymous]
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] AdminOtpVerifyRequest request)
    {
        var result = await adminAuthService.VerifyOtpAsync(request.Email, request.Code);
        if (!result.IsSuccessful) return Unauthorized(new { message = result.Message });
        return Ok(result.Data);
    }

    /// <summary>Exchanges a refresh token for a new access token and a rotated refresh token.</summary>
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] AdminRefreshTokenRequest request)
    {
        var result = await adminAuthService.RefreshTokenAsync(request.RefreshToken);
        if (result == null) return Unauthorized(new { message = ResponseMessages.InvalidRefreshToken });
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
