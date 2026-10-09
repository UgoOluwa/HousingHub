using HousingHub.Core.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.Commons.Email;

/// <summary>
/// Sends an email's links back to whichever of our front ends asked for them.
/// </summary>
/// <remarks>
/// <para>
/// One API serves several front ends — production, the Vercel preview, a developer's
/// localhost. A single configured base URL meant a tester who asked for a password
/// reset on the preview received a link into production, where the token does not
/// exist. The link looked right and failed as "this link has expired", which is a
/// miserable thing to debug from the outside.
/// </para>
/// <para>
/// <b>The origin is matched against an allowlist, never reflected.</b> Building a
/// link on an unchecked <c>Origin</c> header is an account takeover: request a reset
/// for somebody else's address with <c>Origin: https://evil.example</c>, and we send
/// that person a genuine Housing Hub email carrying their own reset token to a site
/// the attacker controls. The allowlist is <c>Cors:AllowedOrigins</c>, which already
/// answers "is this one of our front ends" and is required in production — a second
/// list would drift from it.
/// </para>
/// <para>
/// Anything unrecognised falls back to the configured <c>Email:BaseUrl</c> rather
/// than failing. Mail sent from a background job has no request to read, and that is
/// normal rather than exceptional — expiry warnings and settlement notices are sent
/// on a timer.
/// </para>
/// </remarks>
internal sealed class FrontEndBaseUrlResolver : IFrontEndBaseUrlResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FrontEndBaseUrlResolver> _logger;

    public FrontEndBaseUrlResolver(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<FrontEndBaseUrlResolver> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public string Resolve()
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        if (request is not null)
        {
            var allowed = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            // Origin first: browsers set it on every cross-origin request and it
            // carries no path, so there is nothing to strip. Referer is the fallback
            // for the few requests that omit Origin, and TrustedOrigins returns only
            // the authority, so a page's path never leaks into a link.
            var fromOrigin = TrustedOrigins.Match(request.Headers.Origin.FirstOrDefault(), allowed);
            if (fromOrigin is not null) return fromOrigin;

            var fromReferer = TrustedOrigins.Match(request.Headers.Referer.FirstOrDefault(), allowed);
            if (fromReferer is not null) return fromReferer;

            // Logged, because the commonest cause is a real misconfiguration: a new
            // deployment whose origin nobody added to Cors:AllowedOrigins, which will
            // otherwise send its users to a different environment and look like an
            // expired-link bug.
            var claimed = request.Headers.Origin.FirstOrDefault() ?? request.Headers.Referer.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(claimed))
            {
                _logger.LogWarning(
                    "Email links fell back to the default base URL: {Origin} is not in Cors:AllowedOrigins",
                    claimed);
            }
        }

        return Default();
    }

    private string Default()
    {
        var configured = _configuration["Email:BaseUrl"];

        return string.IsNullOrWhiteSpace(configured)
            ? "https://localhost"
            : configured.TrimEnd('/');
    }
}
