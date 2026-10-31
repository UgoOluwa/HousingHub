using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.Commons.Payments;

/// <summary>
/// Decides where the gateway may send a payer back to.
/// </summary>
/// <remarks>
/// <para>
/// Unchecked, this is an open redirect with a payment attached: the gateway sends
/// the payer wherever this says once they have paid, so an attacker who can get
/// somebody to start a payment with their URL lands that person on a page of their
/// choosing at the exact moment they are expecting a receipt.
/// </para>
/// <para>
/// Validated against <c>Cors:AllowedOrigins</c> rather than a list of its own: that
/// already answers "is this one of our front ends", it is required in production,
/// and a second list would drift from it. A URL that does not match is dropped
/// rather than refused — the payment is perfectly valid without a return URL, and
/// the gateway shows its own receipt page.
/// </para>
/// </remarks>
public interface ICallbackUrlResolver
{
    string? Resolve(string? requested);
}

/// <inheritdoc cref="ICallbackUrlResolver"/>
public class PaymentCallbackUrlResolver : ICallbackUrlResolver
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentCallbackUrlResolver> _logger;

    public PaymentCallbackUrlResolver(
        IConfiguration configuration, ILogger<PaymentCallbackUrlResolver> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string? Resolve(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return null;

        if (!Uri.TryCreate(requested, UriKind.Absolute, out var candidate))
        {
            _logger.LogWarning("Discarded a payment callback URL that is not absolute");
            return null;
        }

        if (candidate.Scheme != Uri.UriSchemeHttps && !candidate.IsLoopback)
        {
            _logger.LogWarning("Discarded a non-HTTPS payment callback URL");
            return null;
        }

        var allowed = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        foreach (var origin in allowed)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var trusted)) continue;

            // Scheme, host and port must all match. Comparing the string prefix
            // instead would let "https://housinghub.ng.attacker.example" through.
            if (candidate.Scheme == trusted.Scheme
                && string.Equals(candidate.Host, trusted.Host, StringComparison.OrdinalIgnoreCase)
                && candidate.Port == trusted.Port)
            {
                return requested;
            }
        }

        _logger.LogWarning(
            "Discarded a payment callback URL for untrusted origin {Origin}",
            candidate.GetLeftPart(UriPartial.Authority));

        return null;
    }
}
