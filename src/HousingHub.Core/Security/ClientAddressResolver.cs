using System.Net;

namespace HousingHub.Core.Security;

/// <summary>
/// Works out which address a request actually came from.
/// </summary>
/// <remarks>
/// <para>
/// Lives here, taking strings rather than an <c>HttpContext</c>, because it is the
/// input to rate limiting and getting it wrong switches rate limiting off. That is
/// worth a unit test, and a function that needs a web host to exercise does not get
/// one.
/// </para>
/// </remarks>
public static class ClientAddressResolver
{
    /// <summary>
    /// The caller's address, taken from the end of <c>X-Forwarded-For</c> that a
    /// client cannot write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rate limiter used to read <c>Split(',')[0]</c> — the leftmost entry. A pen
    /// test found it and reported the login and password-reset limits as bypassable
    /// by rotating the header, which they were: the leftmost entry is whatever the
    /// client sent, so <c>X-Forwarded-For: 1.2.3.4</c> put every attempt in a fresh
    /// bucket and the limit never applied to anything.
    /// </para>
    /// <para>
    /// The list grows left to right, each hop appending the address it received the
    /// connection from. Anything a client invents therefore lands to the <b>left</b>
    /// of the entry the last proxy appended, and the <b>rightmost</b> entry is the one
    /// written by the proxy we trust. Reading from the right is what makes this
    /// unspoofable: a caller can add entries but cannot remove the one describing
    /// them.
    /// </para>
    /// <para>
    /// The cost is that a legitimate proxy in front of a user — a corporate egress, a
    /// VPN, a mobile carrier NAT — shares one bucket for everyone behind it. That is
    /// the right trade on an auth endpoint: throttling a shared egress a little too
    /// hard is recoverable, and a limit any attacker turns off with a header is not a
    /// limit.
    /// </para>
    /// <para>
    /// <b>Only valid behind a proxy that overwrites what it appends</b>, which API
    /// Gateway does. Exposing this API directly would let a client supply the whole
    /// header including the rightmost entry, and this would have to change.
    /// </para>
    /// </remarks>
    /// <param name="forwardedFor">Raw <c>X-Forwarded-For</c> value, or null.</param>
    /// <param name="remoteAddress">The transport-level peer, used when there is no header.</param>
    public static string? Resolve(string? forwardedFor, string? remoteAddress)
    {
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var hops = forwardedFor.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Rightmost entry that actually parses. A junk tail must not fall back to
            // the client-controlled head, so this walks inward rather than giving up —
            // otherwise appending "X-Forwarded-For: 1.2.3.4, notanip" would restore
            // the bypass.
            for (int i = hops.Length - 1; i >= 0; i--)
            {
                if (IPAddress.TryParse(Unbracket(hops[i]), out var parsed))
                    return parsed.ToString();
            }
        }

        return remoteAddress;
    }

    /// <summary>
    /// Strips the brackets and any port from an entry.
    /// </summary>
    /// <remarks>
    /// Some proxies write <c>[2001:db8::1]:443</c> or <c>1.2.3.4:5678</c>. Left alone
    /// those fail to parse, and the loop above would then skip a perfectly good
    /// trusted entry in favour of one further left — which is the direction that
    /// weakens the check.
    /// </remarks>
    private static string Unbracket(string entry)
    {
        if (entry.StartsWith('['))
        {
            var close = entry.IndexOf(']');
            if (close > 1) return entry[1..close];
        }

        // Only strip a port from something with exactly one colon; more than one
        // means a bare IPv6 address, where the colons are part of the address.
        var firstColon = entry.IndexOf(':');
        if (firstColon > 0 && entry.IndexOf(':', firstColon + 1) < 0)
            return entry[..firstColon];

        return entry;
    }
}
