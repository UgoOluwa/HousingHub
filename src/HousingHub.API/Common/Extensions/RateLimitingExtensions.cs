using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace HousingHub.API.Common.Extensions;

/// <summary>
/// Rate limiting for the endpoints an attacker hits in bulk.
/// </summary>
/// <remarks>
/// <para>
/// <b>Known limitation.</b> These limiters are in-memory and therefore per-instance.
/// On Lambda each concurrent execution environment keeps its own counters and they
/// reset on cold start, so the effective limit is looser than configured and an
/// attacker with enough parallelism can exceed it.
/// </para>
/// <para>
/// That is still worth having: it stops naive credential stuffing and accidental
/// client retry storms, and it costs nothing. It is not a substitute for
/// distributed throttling. If real enforcement is needed, add usage plans at API
/// Gateway, which sees every request regardless of which instance serves it.
/// </para>
/// </remarks>
public static class RateLimitingExtensions
{
    /// <summary>Login, register, and other credential-guessing targets.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Endpoints that send an email, where abuse means someone else's inbox suffers.</summary>
    public const string EmailPolicy = "email";

    /// <summary>Chat send — unthrottled, it is a mail bomb with extra steps.</summary>
    public const string MessagingPolicy = "messaging";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Tell the client when to come back rather than leaving them to guess.
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        isSuccessful = false,
                        message = "Too many attempts. Please wait a moment and try again.",
                    },
                    cancellationToken);
            };

            // 10 attempts per minute per IP. Comfortably above a human mistyping a
            // password, far below anything worth calling an attack.
            options.AddPolicy(AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: PartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Tighter, because each request sends mail to an address the caller chose
            // and the cost lands on the recipient and on the Resend quota.
            options.AddPolicy(EmailPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: PartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0,
                    }));

            // Generous enough for a real conversation, bounded enough that a script
            // cannot fire thousands of notification emails at another user.
            options.AddPolicy(MessagingPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: PartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    /// <summary>
    /// Partitions by authenticated user where possible, falling back to client IP.
    /// </summary>
    /// <remarks>
    /// Behind API Gateway or a load balancer, <c>RemoteIpAddress</c> is the proxy
    /// unless forwarded headers are honoured, which would collapse every anonymous
    /// caller into one bucket and lock everyone out together. X-Forwarded-For is read
    /// directly for that reason. It is client-controlled and therefore spoofable —
    /// another reason this is a speed bump rather than a control.
    /// </remarks>
    private static string PartitionKey(HttpContext httpContext)
    {
        var userId = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst("sub")?.Value
            : null;

        if (!string.IsNullOrEmpty(userId)) return $"user:{userId}";

        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ip = string.IsNullOrWhiteSpace(forwarded)
            ? httpContext.Connection.RemoteIpAddress?.ToString()
            : forwarded.Split(',')[0].Trim();

        return $"ip:{ip ?? "unknown"}";
    }
}
