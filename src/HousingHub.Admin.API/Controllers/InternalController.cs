using HousingHub.Core.CustomResponses;
using HousingHub.Service.InspectionService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>
/// Endpoints meant to be triggered by scheduled infrastructure (e.g. an AWS
/// EventBridge Scheduler rule), not by users or the Admin dashboard. Every
/// endpoint here authenticates via a shared secret header instead of the
/// Admin JWT, since a scheduler has no user session to authenticate with.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
public class InternalController(
    IInspectionCommandService inspectionCommandService,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Sends 24-hour reminders (email + an automated Admin chat message) to both
    /// parties for every confirmed inspection due within the next 24 hours that
    /// hasn't been reminded yet. Intended to be invoked on a schedule (e.g. every
    /// 15-30 minutes) — safe to call more often than that, since each inspection
    /// is only ever reminded once.
    /// </summary>
    /// <param name="secret">Must match the configured Internal:WorkerSecret.</param>
    [HttpPost("inspection-reminders/run")]
    [ProducesResponseType(typeof(BaseResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RunInspectionReminders([FromHeader(Name = "X-Worker-Secret")] string? secret)
    {
        var expectedSecret = configuration["Internal:WorkerSecret"];
        if (string.IsNullOrEmpty(expectedSecret) || secret != expectedSecret)
            return Unauthorized();

        var result = await inspectionCommandService.SendDueInspectionRemindersAsync();
        return Ok(result);
    }
}
