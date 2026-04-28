using HousingHub.Core.CustomResponses;
using HousingHub.Service.AdminService;
using HousingHub.Service.Dtos.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.Admin.API.Controllers;

/// <summary>Account settings — manage your own profile and staff members.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminAccountController(IAdminAuthService adminAuthService) : ControllerBase
{
    // ── Own Profile ──────────────────────────────────────────────────────────

    /// <summary>Returns the profile of the currently authenticated admin.</summary>
    /// <response code="200">Admin profile.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(AdminProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile()
    {
        var adminId = GetAdminId();
        if (adminId == Guid.Empty) return Unauthorized();

        var profile = await adminAuthService.GetAdminProfileAsync(adminId);
        if (profile == null) return NotFound();
        return Ok(profile);
    }

    /// <summary>Updates the first and/or last name of the currently authenticated admin.</summary>
    /// <param name="dto">Fields to update. Null fields are left unchanged.</param>
    /// <response code="200">Profile updated.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateAdminProfileDto dto)
    {
        var adminId = GetAdminId();
        if (adminId == Guid.Empty) return Unauthorized();

        var success = await adminAuthService.UpdateAdminProfileAsync(adminId, dto);
        if (!success) return NotFound(new { message = "Admin not found." });
        return Ok(new BaseResponse<bool>(true, true, string.Empty, "Profile updated successfully."));
    }

    /// <summary>Changes the password of the currently authenticated admin.</summary>
    /// <param name="dto">Current and new password.</param>
    /// <response code="200">Password changed.</response>
    /// <response code="400">Current password is incorrect.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPut("password")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangeAdminPasswordDto dto)
    {
        var adminId = GetAdminId();
        if (adminId == Guid.Empty) return Unauthorized();

        var success = await adminAuthService.ChangeAdminPasswordAsync(adminId, dto.CurrentPassword, dto.NewPassword);
        if (!success) return BadRequest(new BaseResponse<bool>(false, false, string.Empty, "Current password is incorrect or admin not found."));
        return Ok(new BaseResponse<bool>(true, true, string.Empty, "Password changed successfully."));
    }

    // ── Staff Management ─────────────────────────────────────────────────────

    /// <summary>Returns a list of all admin staff members.</summary>
    /// <response code="200">List of staff.</response>
    [HttpGet("staff")]
    [ProducesResponseType(typeof(List<AdminStaffDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaff()
    {
        var staff = await adminAuthService.GetAllStaffAsync();
        return Ok(staff);
    }

    /// <summary>Deactivates an admin staff member account.</summary>
    /// <remarks>Deactivated admins cannot log in. Use reactivate to restore access.</remarks>
    /// <param name="id">Staff member's admin ID.</param>
    /// <response code="200">Staff account deactivated.</response>
    /// <response code="404">Staff member not found.</response>
    [HttpPut("staff/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateStaff(Guid id)
    {
        var adminId = GetAdminId();
        if (id == adminId)
            return BadRequest(new BaseResponse<bool>(false, false, string.Empty, "You cannot deactivate your own account."));

        var success = await adminAuthService.DeactivateAdminAsync(id);
        if (!success) return NotFound(new BaseResponse<bool>(false, false, string.Empty, "Staff member not found."));
        return Ok(new BaseResponse<bool>(true, true, string.Empty, "Staff account deactivated."));
    }

    /// <summary>Reactivates a previously deactivated admin staff member account.</summary>
    /// <param name="id">Staff member's admin ID.</param>
    /// <response code="200">Staff account reactivated.</response>
    /// <response code="404">Staff member not found.</response>
    [HttpPut("staff/{id:guid}/reactivate")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateStaff(Guid id)
    {
        var success = await adminAuthService.ReactivateAdminAsync(id);
        if (!success) return NotFound(new BaseResponse<bool>(false, false, string.Empty, "Staff member not found."));
        return Ok(new BaseResponse<bool>(true, true, string.Empty, "Staff account reactivated."));
    }

    private Guid GetAdminId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                 ?? User.FindFirst(JwtRegisteredClaimNames.Sub);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
