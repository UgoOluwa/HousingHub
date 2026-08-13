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
