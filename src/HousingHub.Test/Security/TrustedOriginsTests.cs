using HousingHub.Core.Security;

namespace HousingHub.Test.Security;

/// <summary>
/// Whether a URL is one of our own front ends.
/// </summary>
/// <remarks>
/// Three callers depend on this answer — a payment callback, an OAuth return URL,
/// and the base URL an email link is built on — and each is an open redirect or
/// worse when it is wrong.
/// </remarks>
public class TrustedOriginsTests
{
    private static readonly string[] Allowed =
    [
        "https://housinghub.ng",
        "https://housing-hub.vercel.app",
        "http://localhost:3000",
    ];

    [Fact]
    public void AnAllowedOrigin_Matches()
    {
        Assert.Equal("https://housinghub.ng", TrustedOrigins.Match("https://housinghub.ng", Allowed));
    }

    /// <summary>
    /// Returns the origin, not the URL. A caller building links on the result must
    /// not inherit a path from whatever arrived in a Referer header.
    /// </summary>
    [Fact]
    public void APathOnTheCandidate_IsNotCarriedThrough()
    {
        Assert.Equal(
            "https://housinghub.ng",
            TrustedOrigins.Match("https://housinghub.ng/properties/123?ref=x", Allowed));
    }

    /// <summary>
    /// The attack a prefix comparison lets through: this starts with
    /// "https://housinghub.ng" and belongs to somebody else entirely.
    /// </summary>
    [Fact]
    public void ALookalikeSuffixHost_IsRefused()
    {
        Assert.Null(TrustedOrigins.Match("https://housinghub.ng.attacker.example", Allowed));
    }

    [Fact]
    public void ASubdomainOfAnAllowedHost_IsRefused()
    {
        Assert.Null(TrustedOrigins.Match("https://evil.housinghub.ng", Allowed));
    }

    /// <summary>http://housinghub.ng is a different origin from the https one.</summary>
    [Fact]
    public void TheWrongScheme_IsRefused()
    {
        Assert.Null(TrustedOrigins.Match("http://housinghub.ng", Allowed));
    }

    [Fact]
    public void TheWrongPort_IsRefused()
    {
        Assert.Null(TrustedOrigins.Match("http://localhost:3001", Allowed));
    }

    [Fact]
    public void TheRightPort_Matches()
    {
        Assert.Equal("http://localhost:3000", TrustedOrigins.Match("http://localhost:3000", Allowed));
    }

    /// <summary>Host comparison is case-insensitive; hosts are.</summary>
    [Fact]
    public void ADifferentlyCasedHost_Matches()
    {
        Assert.NotNull(TrustedOrigins.Match("https://HousingHub.NG", Allowed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    public void AnythingThatIsNotAnAbsoluteUrl_IsRefused(string? candidate)
    {
        Assert.Null(TrustedOrigins.Match(candidate, Allowed));
    }

    /// <summary>
    /// A scheme that is not http or https — javascript: being the one that matters —
    /// never matches, whatever the list says.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    public void ANonWebScheme_IsRefused(string candidate)
    {
        Assert.Null(TrustedOrigins.Match(candidate, Allowed));
    }

    /// <summary>
    /// An empty allowlist trusts nothing. It is the state a misconfigured deployment
    /// boots in, and failing closed there is the whole point.
    /// </summary>
    [Fact]
    public void AnEmptyAllowList_TrustsNothing()
    {
        Assert.Null(TrustedOrigins.Match("https://housinghub.ng", []));
        Assert.Null(TrustedOrigins.Match("https://housinghub.ng", null));
    }

    /// <summary>A junk entry in the list is skipped rather than throwing.</summary>
    [Fact]
    public void AnUnparseableEntryInTheList_IsSkipped()
    {
        Assert.NotNull(TrustedOrigins.Match("https://housinghub.ng", ["", "nonsense", "https://housinghub.ng"]));
    }
}
