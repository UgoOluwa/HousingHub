namespace HousingHub.Service.VerificationService.Interfaces;

/// <summary>
/// Finds verifications that have lapsed and takes the badge away.
/// </summary>
/// <remarks>
/// Verification is a statement with a shelf life. LASRERA registrations are
/// annual, so an agent verified last April is making a claim this April that
/// nobody has checked. Without something that actively expires them, a badge
/// granted once is a badge granted forever — which is the same failure as not
/// verifying at all, just slower and more convincing.
/// </remarks>
public interface IVerificationExpiryService
{
    /// <summary>
    /// Expires every approved case whose earliest document has lapsed, and revokes
    /// the tier it granted.
    /// </summary>
    /// <param name="asOf">
    /// Treated as "now". Injectable so the behaviour is testable without waiting a
    /// year, and so a backfill can be run at a chosen point in time.
    /// </param>
    Task<VerificationExpirySummary> ExpireLapsedAsync(DateTime asOf);

    /// <summary>
    /// Warns subjects whose verification is about to lapse, while they can still do
    /// something about it.
    /// </summary>
    /// <remarks>
    /// Renewing a LASRERA registration takes weeks, so finding out on the day the
    /// badge drops is finding out too late. Thirty days is enough notice to start;
    /// seven is the nudge for anyone who filed it and forgot.
    /// </remarks>
    Task<VerificationReminderSummary> SendExpiryRemindersAsync(DateTime asOf);
}

/// <summary>
/// Reminder thresholds, in days before expiry.
/// </summary>
/// <remarks>
/// Descending, because the sweep takes the first threshold a case has crossed and
/// a case three days from expiry should get the seven-day nudge rather than the
/// thirty-day one it already missed.
/// </remarks>
public static class ExpiryReminderThresholds
{
    public static readonly IReadOnlyList<int> DaysBefore = [30, 7];
}

/// <summary>What one reminder pass did.</summary>
/// <param name="Examined">Approved cases carrying an expiry that were considered.</param>
/// <param name="Sent">Reminders actually sent.</param>
/// <param name="Failed">
/// Reminders that could not be sent. These are retried on the next run, because the
/// threshold is only recorded after a successful send.
/// </param>
public record VerificationReminderSummary(int Examined, int Sent, int Failed)
{
    public static VerificationReminderSummary Empty => new(0, 0, 0);
}

/// <summary>
/// What one sweep did.
/// </summary>
/// <param name="Examined">Approved cases carrying an expiry that were considered.</param>
/// <param name="Expired">Cases moved to Expired.</param>
/// <param name="TiersRevoked">Subjects whose badge was actually taken away.</param>
/// <param name="Failed">
/// Cases that could not be processed. Non-zero means somebody is still showing a
/// badge they are no longer entitled to, which is worth alerting on rather than
/// leaving in a log.
/// </param>
public record VerificationExpirySummary(int Examined, int Expired, int TiersRevoked, int Failed)
{
    public static VerificationExpirySummary Empty => new(0, 0, 0, 0);
}
