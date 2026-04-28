using HousingHub.Service.AdminService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminAuthController(IAdminAuthService adminAuthService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        var result = await adminAuthService.LoginAsync(request.Email, request.Password);
        if (result == null) return Unauthorized(new { message = "Invalid credentials" });
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

public record AdminLoginRequest(string Email, string Password);
public record CreateAdminRequest(string SeedKey, string Email, string Password, string FirstName, string LastName);
