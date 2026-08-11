using Microsoft.Extensions.Configuration;
using Sentry;

namespace HousingHub.Core.Observability;

/// <summary>
/// Shared Sentry configuration for both APIs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Quota.</b> The free Developer plan allows 5,000 events a month across the
/// whole organisation, shared by four projects. A single recurring error can burn
/// the month in a day, after which Sentry silently stops accepting events and you
/// are blind again without being told. Everything below is shaped by that: only
/// things that are actually bugs get sent.
/// </para>
/// <para>
/// <b>PII.</b> These APIs carry national ID numbers, KYC object keys, JWTs and the
/// worker secret. Under NDPA, shipping any of that to a third-party processor is a
/// reportable problem rather than an embarrassment. <c>SendDefaultPii</c> stays
/// false and <see cref="Scrub"/> strips the headers and query values where
/// credentials actually appear.
/// </para>
/// </remarks>
public static class SentryOptionsConfigurator
{
    /// <summary>Header names that carry credentials. Compared case-insensitively.</summary>
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Worker-Secret",
        "X-Api-Key",
    };

    /// <summary>
    /// Substrings marking a value that must never be transmitted.
    /// </summary>
    /// <remarks>
    /// <c>x-amz-</c> and <c>signature</c> catch presigned S3 URLs. The signature in
    /// one of those <i>is</i> the credential — a captured link grants whoever holds
    /// it read access to a KYC identity document for the URL's lifetime.
    /// </remarks>
    private static readonly string[] SensitiveKeyFragments =
    [
        "token", "password", "secret", "authorization", "apikey", "api_key",
        "nationalid", "national_id", "iddocument", "bvn", "nin",
        "x-amz-", "signature",
    ];

    private static bool IsSensitive(string key) =>
        SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Applies the shared configuration.
    /// </summary>
    /// <param name="options">The options instance being built.</param>
    /// <param name="configuration">Used to read <c>Sentry:Dsn</c> and the environment name.</param>
    /// <param name="release">Optional build identifier so an error can be tied to a deploy.</param>
    public static void Configure(SentryOptions options, IConfiguration configuration, string? release = null)
    {
        var dsn = configuration["Sentry:Dsn"];

        // No DSN means the SDK is inert rather than broken. A missing environment
        // variable should cost you monitoring, never a boot failure or a stream of
        // transport errors in the logs.
        options.Dsn = string.IsNullOrWhiteSpace(dsn) ? null : dsn;
        options.Environment = configuration["Sentry:Environment"] ?? "production";
        if (!string.IsNullOrWhiteSpace(release)) options.Release = release;

        options.SendDefaultPii = false;
        options.MaxBreadcrumbs = 30;

        // Performance tracing bills against a separate span quota the free plan
        // barely covers, and there is no performance question we are trying to
        // answer yet. Errors are the point.
        options.TracesSampleRate = 0.0;

        // Lambda freezes the execution environment the moment a response is
        // returned. Without a flush window, the event queued during a failed
        // request is still in memory when the process is suspended and is simply
        // lost — you would see the 500 in CloudWatch and nothing in Sentry.
        options.FlushTimeout = TimeSpan.FromSeconds(3);

        options.SetBeforeSend(Scrub);
    }

    /// <summary>
    /// Last gate before an event leaves the process.
    /// </summary>
    private static SentryEvent? Scrub(SentryEvent @event, SentryHint _)
    {
        if (@event.Request is { } request)
        {
            foreach (var name in request.Headers.Keys.ToList())
            {
                if (SensitiveHeaders.Contains(name)) request.Headers[name] = "[redacted]";
            }

            if (!string.IsNullOrEmpty(request.QueryString))
            {
                request.QueryString = ScrubQueryString(request.QueryString);
            }

            // Belt and braces: MaxRequestBodySize.None should mean this is never
            // populated, but the cost of being wrong here is somebody's identity
            // document in a third-party system.
            request.Data = null;
        }

        foreach (var key in @event.Extra.Keys.ToList())
        {
            if (IsSensitive(key)) @event.SetExtra(key, "[redacted]");
        }

        foreach (var key in @event.Tags.Keys.ToList())
        {
            if (IsSensitive(key)) @event.SetTag(key, "[redacted]");
        }

        return @event;
    }

    /// <summary>
    /// Blanks the values of sensitive query parameters, keeping their names so the
    /// shape of the request is still legible when debugging.
    /// </summary>
    private static string ScrubQueryString(string queryString)
    {
        var parts = queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);

        var scrubbed = parts.Select(part =>
        {
            var separator = part.IndexOf('=');
            if (separator <= 0) return part;

            var name = part[..separator];
            return IsSensitive(name) ? $"{name}=[redacted]" : part;
        });

        return string.Join('&', scrubbed);
    }
}
