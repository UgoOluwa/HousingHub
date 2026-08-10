using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace HousingHub.Admin.API.Common;

/// <summary>
/// Rate limiting for the admin authentication surface.
/// </summary>
/// <remarks>
/// Admin sign-in is OTP-only, so the six-digit code <i>is</i> the credential. Without
/// a limit, the per-code attempt counter is trivially defeated by requesting a fresh
/// code and continuing to guess.
///
/// Same caveat as the consumer API: these limiters are in-memory and per-instance, so
/// on Lambda the effective limit is looser than configured and resets on cold start.
/// Real enforcement belongs at API Gateway.
/// </remarks>
public static class RateLimitingExtensions
{
    public const string OtpRequestPolicy = "admin-otp-request";
    public const string OtpVerifyPolicy = "admin-otp-verify";

    /// <summary>
    /// Endpoints authenticated by the shared worker secret rather than a JWT.
    /// </summary>
    public const string InternalWorkerPolicy = "internal-worker";

    public static IServiceCollection AddAdminRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "Too many attempts. Please wait a moment and try again." },
                    cancellationToken);
            };

            // Requesting a code sends mail; the service already applies a 60s
            // per-account cooldown, this bounds it per caller as well.
            options.AddPolicy(OtpRequestPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: PartitionKey(http),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0,
                    }));

            // A six-digit code is one-in-a-million per guess. Ten attempts a minute
            // keeps brute force firmly out of reach while leaving room for typos.
            options.AddPolicy(OtpVerifyPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: PartitionKey(http),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // The worker secret is a static, long-lived credential with no lockout and
            // no second factor, and one of the endpoints behind it grants SuperAdmin.
            // A scheduler calls these on the order of once every fifteen minutes, so a
            // limit this tight costs legitimate callers nothing while removing the
            // ability to guess the secret at speed.
            options.AddPolicy(InternalWorkerPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: PartitionKey(http),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    /// <summary>
    /// Partitions by client IP. X-Forwarded-For is read directly because behind API
    /// Gateway RemoteIpAddress is the proxy, which would collapse every caller into a
    /// single bucket and lock all admins out at once.
    /// </summary>
    private static string PartitionKey(HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ip = string.IsNullOrWhiteSpace(forwarded)
            ? http.Connection.RemoteIpAddress?.ToString()
            : forwarded.Split(',')[0].Trim();

        return $"ip:{ip ?? "unknown"}";
    }
}
