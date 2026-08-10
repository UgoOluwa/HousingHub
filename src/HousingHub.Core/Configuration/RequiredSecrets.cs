using Microsoft.Extensions.Configuration;

namespace HousingHub.Core.Configuration;

/// <summary>
/// Startup validation for configuration values that must never fall back to a
/// default committed to source control.
/// </summary>
/// <remarks>
/// Both APIs previously read secrets with the null-forgiving operator
/// (<c>configuration["Jwt:Secret"]!</c>) and never checked the result. If an
/// environment variable was missing or misnamed at deploy time, the service
/// booted happily and signed every token with the placeholder string sitting in
/// appsettings.json — a value visible to anyone with repository access.
///
/// Failing at startup is the correct behaviour here: a service that cannot
/// authenticate securely should not accept traffic at all.
/// </remarks>
public static class RequiredSecrets
{
    /// <summary>
    /// Substrings that indicate a value is a committed placeholder rather than a
    /// real secret. Matched case-insensitively.
    /// </summary>
    private static readonly string[] PlaceholderMarkers =
    [
        "your-",
        "replace-this",
        "replace_me",
        "changeme",
        "change-me",
        "xxxxx",
        "placeholder",
        "todo",
    ];

    /// <summary>Minimum length for an HMAC signing key (256 bits as UTF-8 text).</summary>
    private const int MinimumSigningKeyLength = 32;

    /// <summary>
    /// Throws unless every listed key holds a usable secret.
    /// </summary>
    /// <param name="configuration">The built configuration.</param>
    /// <param name="signingKeys">
    /// Keys that are used as HMAC signing material and are additionally length-checked.
    /// </param>
    /// <param name="otherRequired">Keys that must simply be present and non-placeholder.</param>
    /// <param name="requiredArrays">
    /// Configuration sections that must bind to a non-empty array. Unlike a missing
    /// secret, an empty array here fails silently and confusingly rather than loudly:
    /// an empty <c>Cors:AllowedOrigins</c> boots fine, then rejects every browser
    /// request and every Google sign-in (the returnUrl allow-list is derived from it)
    /// with no indication of why.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Lists every offending key at once, so a misconfigured deployment is fixed in
    /// one pass rather than one restart per missing value.
    /// </exception>
    public static void Validate(
        IConfiguration configuration,
        IEnumerable<string> signingKeys,
        IEnumerable<string> otherRequired,
        IEnumerable<string>? requiredArrays = null)
    {
        var problems = new List<string>();

        foreach (var key in signingKeys)
        {
            var value = configuration[key];

            if (IsMissingOrPlaceholder(value))
            {
                problems.Add($"{key} is missing or is still a placeholder.");
            }
            else if (value!.Length < MinimumSigningKeyLength)
            {
                problems.Add(
                    $"{key} is {value.Length} characters; signing keys must be at least " +
                    $"{MinimumSigningKeyLength}.");
            }
        }

        foreach (var key in otherRequired)
        {
            if (IsMissingOrPlaceholder(configuration[key]))
            {
                problems.Add($"{key} is missing or is still a placeholder.");
            }
        }

        foreach (var key in requiredArrays ?? [])
        {
            // GetChildren rather than Get<string[]>(): this project references only
            // Configuration.Abstractions, and the generic binder lives in a separate
            // package. Enumerating children needs nothing extra and reads an array
            // section identically — the entries are just numerically-keyed children.
            var values = configuration.GetSection(key).GetChildren()
                .Select(child => child.Value)
                .ToList();

            if (values.Count == 0 || values.All(string.IsNullOrWhiteSpace))
            {
                problems.Add($"{key} is empty; at least one entry is required.");
            }
        }

        if (problems.Count == 0) return;

        throw new InvalidOperationException(
            "Refusing to start — required configuration is missing or unsafe:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, problems.Select(p => "  - " + p))
            + Environment.NewLine
            + Environment.NewLine
            + "Supply these via environment variables or AWS Secrets Manager. "
            + "For local development use user-secrets; do not put real values in appsettings files.");
    }

    private static bool IsMissingOrPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        return PlaceholderMarkers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
