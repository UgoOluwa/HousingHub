namespace HousingHub.Core.Security;

/// <summary>
/// Decides whether a URL belongs to one of our own front ends.
/// </summary>
/// <remarks>
/// <para>
/// The question "is this one of ours" is asked in three places — a payment callback,
/// an OAuth return URL, and the base URL an email link is built on — and every one
/// of them is an open redirect or worse if the answer is wrong. Until this existed
/// there were two copies of the rule and a third about to be written.
/// </para>
/// <para>
/// Scheme, host and port must all match. A prefix comparison is the classic mistake
/// here: <c>https://housinghub.ng.attacker.example</c> starts with
/// <c>https://housinghub.ng</c> and is not remotely the same site.
/// </para>
/// </remarks>
public static class TrustedOrigins
{
    /// <summary>
    /// The candidate's origin when it matches one of <paramref name="allowed"/>, else null.
    /// </summary>
    /// <remarks>
    /// Returns the origin rather than the whole URL, so a caller building links on it
    /// cannot accidentally inherit a path or a query string from whatever was sent in.
    /// </remarks>
    public static string? Match(string? candidateUrl, IEnumerable<string>? allowed)
    {
        if (string.IsNullOrWhiteSpace(candidateUrl) || allowed is null) return null;

        if (!Uri.TryCreate(candidateUrl, UriKind.Absolute, out var candidate)) return null;

        if (candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps)
            return null;

        foreach (var origin in allowed)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var trusted)) continue;

            if (string.Equals(candidate.Scheme, trusted.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Host, trusted.Host, StringComparison.OrdinalIgnoreCase)
                && candidate.Port == trusted.Port)
            {
                return candidate.GetLeftPart(UriPartial.Authority);
            }
        }

        return null;
    }

    /// <summary>Whether the URL's origin is one of ours.</summary>
    public static bool IsTrusted(string? candidateUrl, IEnumerable<string>? allowed) =>
        Match(candidateUrl, allowed) is not null;
}
