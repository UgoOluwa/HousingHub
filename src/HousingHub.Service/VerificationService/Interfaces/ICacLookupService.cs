namespace HousingHub.Service.VerificationService.Interfaces;

/// <summary>
/// Looks up a company registration against the Corporate Affairs Commission.
/// </summary>
/// <remarks>
/// <para>
/// The one genuinely automatable check in business verification. Dojah, QoreID and
/// Mono all sell a CAC lookup; the interface exists so choosing between them, or
/// switching later, is one class rather than a change to the review flow.
/// </para>
/// <para>
/// <b>Advisory only.</b> The result never decides a case. Two reasons, and both
/// matter. Nigerian registry data is inconsistent enough that a failed lookup is
/// often a stale record rather than a bad applicant — declining on that basis
/// would reject real companies. And a <i>passing</i> lookup only confirms the
/// number exists; it says nothing about whether the person submitting it has any
/// connection to that company, which is the fraud we actually care about. The
/// human decision stays authoritative and this informs it.
/// </para>
/// </remarks>
public interface ICacLookupService
{
    /// <summary>Provider name recorded on the document, e.g. "QoreID". Null when none is configured.</summary>
    string? ProviderName { get; }

    /// <summary>
    /// Looks up a registration number.
    /// </summary>
    /// <param name="registrationNumber">RC or BN number as printed on the certificate.</param>
    /// <param name="expectedCompanyName">
    /// The name the applicant claims. Compared loosely — a mismatch is reported for
    /// a human to weigh, not treated as a failure.
    /// </param>
    Task<CacLookupResult> LookupAsync(string registrationNumber, string? expectedCompanyName, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a CAC lookup found.
/// </summary>
/// <param name="Performed">
/// False when no provider is configured, or the provider could not be reached. The
/// caller must treat this differently from a failed check: "we did not look" is not
/// "we looked and it was wrong".
/// </param>
/// <param name="Found">Whether the registration number resolved to a company.</param>
/// <param name="RegisteredName">Company name as held by the registry.</param>
/// <param name="NameMatches">
/// Whether <c>RegisteredName</c> resembles the name the applicant claimed. Null when
/// no comparison was possible.
/// </param>
/// <param name="Status">Registry status — "ACTIVE", "INACTIVE" — where the provider supplies one.</param>
/// <param name="RawResponse">
/// Provider payload, kept for the audit trail: when a verification decision is
/// disputed later, "what did the registry say on the day" is the question, and
/// providers change their responses over time.
/// </param>
public record CacLookupResult(
    bool Performed,
    bool Found,
    string? RegisteredName = null,
    bool? NameMatches = null,
    string? Status = null,
    string? RawResponse = null)
{
    /// <summary>No provider configured, or the provider was unreachable.</summary>
    public static CacLookupResult NotPerformed() => new(Performed: false, Found: false);
}
