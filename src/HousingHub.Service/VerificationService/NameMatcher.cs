namespace HousingHub.Service.VerificationService;

/// <summary>
/// Compares a name on a document against a name on an account.
/// </summary>
/// <remarks>
/// <para>
/// This is the check that catches the fraud that actually happens here. Forged
/// documents are rare; <i>real</i> documents belonging to someone else are common
/// — a genuine CAC certificate, a genuine Certificate of Occupancy, submitted by a
/// person with no connection to it. Comparing the name on the paper to the
/// verified account holder is what surfaces that.
/// </para>
/// <para>
/// <b>It reports, it does not decide.</b> Nigerian names legitimately vary between
/// documents in ways an exact comparison would flag as fraud: a middle name on one
/// and not the other, a maiden name, an anglicised spelling, an initial, a
/// diacritic dropped by a registry that only accepts ASCII. Auto-rejecting on a
/// string mismatch would decline honest applicants at a high rate. The output
/// feeds <see cref="Model.Enums.VerificationCaseStatus.EscalatedNameMismatch"/>,
/// which is a prompt for a human, not a verdict.
/// </para>
/// </remarks>
public static class NameMatcher
{
    /// <summary>How closely two names correspond.</summary>
    public enum MatchLevel
    {
        /// <summary>One side was missing. Nothing can be concluded.</summary>
        Unknown = 0,

        /// <summary>Same name tokens, ignoring order, case and punctuation.</summary>
        Exact = 1,

        /// <summary>
        /// One is contained in the other, or they share every token one holds.
        /// Typically a missing middle name — usually fine, worth a glance.
        /// </summary>
        Partial = 2,

        /// <summary>No meaningful overlap. Worth escalating.</summary>
        None = 3,
    }

    /// <summary>
    /// Words that carry no identifying information and would otherwise create false
    /// matches between two entirely different companies.
    /// </summary>
    private static readonly HashSet<string> NoiseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        // Corporate forms
        "ltd", "limited", "plc", "llc", "enterprises", "enterprise", "ventures",
        "venture", "nigeria", "nig", "company", "co", "and", "&", "the",
        "global", "international", "services", "service", "group", "holdings",
        // Personal honorifics
        "mr", "mrs", "miss", "ms", "dr", "engr", "chief", "alhaji", "alhaja",
        "prince", "princess", "barr", "arc", "esv", "pastor", "rev",
    };

    /// <summary>
    /// Splits a name into comparable tokens: lowercased, punctuation removed,
    /// single initials and noise words discarded.
    /// </summary>
    /// <remarks>
    /// Single letters are dropped because "Chukwuemeka O. Nwosu" and "Chukwuemeka
    /// Nwosu" are the same person, and keeping the initial would make them differ.
    /// </remarks>
    private static HashSet<string> Tokenise(string value)
    {
        var cleaned = new string(value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray());

        return cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 1)
            .Where(token => !NoiseTokens.Contains(token))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Compares two names.
    /// </summary>
    /// <returns>
    /// <see cref="MatchLevel.Unknown"/> when either side is blank, or when
    /// stripping noise words leaves nothing to compare — which happens with a
    /// company called something like "Nigeria Global Services Ltd", where every
    /// token is generic. Reporting Unknown there is honest; reporting a match on
    /// noise alone would be worse than saying nothing.
    /// </returns>
    public static MatchLevel Compare(string? nameOnDocument, string? nameOnAccount)
    {
        if (string.IsNullOrWhiteSpace(nameOnDocument) || string.IsNullOrWhiteSpace(nameOnAccount))
            return MatchLevel.Unknown;

        var documentTokens = Tokenise(nameOnDocument);
        var accountTokens = Tokenise(nameOnAccount);

        if (documentTokens.Count == 0 || accountTokens.Count == 0)
            return MatchLevel.Unknown;

        if (documentTokens.SetEquals(accountTokens))
            return MatchLevel.Exact;

        // Every token of the shorter name appearing in the longer one is the
        // missing-middle-name case, and the overwhelmingly common benign variation.
        if (documentTokens.IsSubsetOf(accountTokens) || accountTokens.IsSubsetOf(documentTokens))
            return MatchLevel.Partial;

        // A shared surname alone is weak evidence in a market with common family
        // names, so partial credit requires more than one token in common.
        return documentTokens.Intersect(accountTokens).Count() > 1
            ? MatchLevel.Partial
            : MatchLevel.None;
    }

    /// <summary>
    /// Whether a reviewer should be prompted to treat this as possible
    /// impersonation.
    /// </summary>
    /// <remarks>
    /// Only <see cref="MatchLevel.None"/>. Unknown is not suspicious — it means we
    /// could not compare, which is a data gap rather than a red flag, and treating
    /// it as one would bury real mismatches in noise.
    /// </remarks>
    public static bool ShouldEscalate(MatchLevel level) => level == MatchLevel.None;
}
