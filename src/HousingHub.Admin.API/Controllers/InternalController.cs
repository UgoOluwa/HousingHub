using HousingHub.Admin.API.Common;
using HousingHub.Core.CustomResponses;
using HousingHub.Core.Security;
using HousingHub.Service.AdminService;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.VerificationService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HousingHub.Admin.API.Controllers;

/// <summary>
/// Endpoints meant to be triggered by scheduled infrastructure (e.g. an AWS
/// EventBridge Scheduler rule), not by users or the Admin dashboard. Every
/// endpoint here authenticates via a shared secret header instead of the
/// Admin JWT, since a scheduler has no user session to authenticate with.
/// </summary>
/// <remarks>
/// A static shared secret is a weak credential: it never expires, it has no
/// second factor, and it is the same for every caller. Two things compensate.
/// The secret is compared in constant time, so a wrong guess reveals nothing
/// about how close it was. And the whole controller is rate limited, so it
/// cannot be guessed at speed — which matters far more than the constant-time
/// comparison does, and was the actual gap.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.InternalWorkerPolicy)]
public class InternalController(
    IInspectionCommandService inspectionCommandService,
    IAdminAuthService adminAuthService,
    IVerificationExpiryService verificationExpiryService,
    IConfiguration configuration,
    ILogger<InternalController> logger) : ControllerBase
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
        if (!IsAuthorisedWorker(secret))
            return Unauthorized();

        var result = await inspectionCommandService.SendDueInspectionRemindersAsync();
        return Ok(result);
    }

    /// <summary>
    /// Daily verification maintenance: expire what has lapsed, warn what is about to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Intended to run once a day. Verification is a claim with a shelf life —
    /// LASRERA registrations are annual — and without something that actively expires
    /// them, a badge granted once is granted forever. That is the same failure as not
    /// verifying at all, just slower and more convincing to whoever relies on it.
    /// </para>
    /// <para>
    /// Idempotent: a case already moved to Expired is skipped, so running it more
    /// often than daily is harmless. Daily is enough — a badge lingering for a few
    /// hours past its expiry is not the risk; lingering for months is.
    /// </para>
    /// <para>
    /// Both halves run in one call because they read the same index and belong to the
    /// same job. Expiring runs first, so a case that lapsed overnight is expired
    /// rather than warned that it expires in seven days.
    /// </para>
    /// <para>
    /// The response reports failures separately. A non-zero <c>failed</c> on the
    /// expiry side means somebody is still showing a badge they are no longer
    /// entitled to; on the reminder side it means a warning did not reach someone
    /// whose badge is about to drop. Both are worth alerting on rather than leaving
    /// in a log.
    /// </para>
    /// </remarks>
    /// <param name="secret">Must match the configured Internal:WorkerSecret.</param>
    [HttpPost("verification-expiry/run")]
    [ProducesResponseType(typeof(BaseResponse<VerificationMaintenanceSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RunVerificationExpiry(
        [FromHeader(Name = "X-Worker-Secret")] string? secret)
    {
        if (!IsAuthorisedWorker(secret))
            return Unauthorized();

        var now = DateTime.UtcNow;

        // Expire first, remind second. Both read the same index, and doing them in
        // this order means a case that lapsed overnight is expired rather than sent
        // a "expires in 7 days" warning it has already outlived.
        var expiry = await verificationExpiryService.ExpireLapsedAsync(now);
        var reminders = await verificationExpiryService.SendExpiryRemindersAsync(now);

        var result = new VerificationMaintenanceSummary(expiry, reminders);

        return Ok(new BaseResponse<VerificationMaintenanceSummary>(
            result, true, string.Empty,
            $"Expired {expiry.Expired} (revoked {expiry.TiersRevoked}, failed {expiry.Failed}); "
            + $"reminded {reminders.Sent} (failed {reminders.Failed})."));
    }

    /// <summary>
    /// One-time bootstrap: promotes an existing admin to SuperAdmin. Needed because
    /// the very first SuperAdmin can't grant themselves the role through the normal
    /// (SuperAdmin-only) staff management endpoints — someone has to be first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Disabled unless explicitly switched on.</b> This endpoint grants the highest
    /// privilege in the system, and it was reachable in every environment, permanently,
    /// behind nothing but a header — for a bootstrap step that by definition runs once.
    /// </para>
    /// <para>
    /// It is now gated on <c>Internal:EnableSuperAdminBootstrap</c>, which defaults to
    /// false. To use it: set the flag true, deploy, promote the account, set it back to
    /// false, deploy again. Deleting the endpoint outright would have been cleaner, but
    /// it risks locking you out permanently if no SuperAdmin exists yet — a flag is
    /// reversible and a deletion is not.
    /// </para>
    /// <para>
    /// When disabled this returns 404 rather than 403. A 403 would confirm the route
    /// exists and is merely switched off, which tells an attacker exactly what to watch
    /// for; a 404 is indistinguishable from the endpoint never having been written.
    /// </para>
    /// </remarks>
    /// <param name="secret">Must match the configured Internal:WorkerSecret.</param>
    /// <param name="email">Email of the admin to promote.</param>
    [HttpPut("admins/promote")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PromoteToSuperAdmin([FromHeader(Name = "X-Worker-Secret")] string? secret, [FromQuery] string email)
    {
        if (!configuration.GetValue<bool>("Internal:EnableSuperAdminBootstrap"))
            return NotFound();

        if (!IsAuthorisedWorker(secret))
            return Unauthorized();

        var success = await adminAuthService.PromoteToSuperAdminAsync(email);
        if (!success) return NotFound(new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("admin")));
        return Ok(new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.AdminPromotedToSuperAdmin));
    }

    /// <summary>
    /// True when the caller presented the configured worker secret.
    /// </summary>
    /// <remarks>
    /// An unset or empty <c>Internal:WorkerSecret</c> denies everything rather than
    /// allowing everything. Startup validation should already have refused to boot in
    /// that state (see <c>RequiredSecrets</c>), so this is a second line of defence
    /// against a configuration path that bypasses it.
    /// </remarks>
    private bool IsAuthorisedWorker(string? presented)
    {
        var expected = configuration["Internal:WorkerSecret"];

        if (SecretComparer.FixedTimeEquals(presented, expected)) return true;

        // A bare 401 here is undiagnosable: both sides of the comparison are stored
        // as secrets and displayed as dots, so there is no way to see which is wrong
        // or why. This says what shape the mismatch has without printing either
        // value — see SecretComparer.DescribeMismatch.
        //
        // Warning rather than Error on purpose. This fires on every unauthorised
        // probe of a public endpoint, and routing that to Sentry would burn the
        // free-tier quota on internet background noise.
        logger.LogWarning(
            "Internal worker call rejected: {Reason}",
            SecretComparer.DescribeMismatch(presented, expected));

        return false;
    }
}

/// <summary>
/// Combined result of one daily verification maintenance run.
/// </summary>
/// <param name="Expiry">Badges taken away because their evidence lapsed.</param>
/// <param name="Reminders">Warnings sent to people whose evidence lapses soon.</param>
public record VerificationMaintenanceSummary(
    VerificationExpirySummary Expiry,
    VerificationReminderSummary Reminders);
