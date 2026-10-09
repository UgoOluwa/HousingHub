using HousingHub.Service.Commons.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HousingHub.Test.Email;

/// <summary>
/// Which front end an email's links point at.
/// </summary>
/// <remarks>
/// One API serves production, the Vercel preview and a developer's localhost. A
/// single configured base URL sent a tester's password-reset link into production,
/// where the token does not exist — and it surfaced as "this link has expired",
/// which is close to undebuggable from the outside.
/// </remarks>
public class FrontEndBaseUrlResolverTests
{
    private const string Production = "https://housinghub.ng";
    private const string Preview = "https://housing-hub.vercel.app";

    private static IConfiguration Config(string? defaultBaseUrl = Production) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Email:BaseUrl", defaultBaseUrl },
                { "Cors:AllowedOrigins:0", Production },
                { "Cors:AllowedOrigins:1", Preview },
                { "Cors:AllowedOrigins:2", "http://localhost:3000" },
            })
            .Build();

    private static FrontEndBaseUrlResolver Build(
        string? origin = null, string? referer = null, IConfiguration? configuration = null)
    {
        var accessor = new HttpContextAccessor();

        if (origin is not null || referer is not null)
        {
            var context = new DefaultHttpContext();
            if (origin is not null) context.Request.Headers.Origin = origin;
            if (referer is not null) context.Request.Headers.Referer = referer;
            accessor.HttpContext = context;
        }

        return new FrontEndBaseUrlResolver(
            accessor,
            configuration ?? Config(),
            NullLogger<FrontEndBaseUrlResolver>.Instance);
    }

    /// <summary>The reported bug: a reset started on the preview must stay there.</summary>
    [Fact]
    public void ARequestFromThePreview_GetsPreviewLinks()
    {
        Assert.Equal(Preview, Build(origin: Preview).Resolve());
    }

    [Fact]
    public void ARequestFromProduction_GetsProductionLinks()
    {
        Assert.Equal(Production, Build(origin: Production).Resolve());
    }

    [Fact]
    public void ARequestFromLocalhost_GetsLocalhostLinks()
    {
        Assert.Equal("http://localhost:3000", Build(origin: "http://localhost:3000").Resolve());
    }

    /// <summary>
    /// The account takeover this allowlist exists to stop: ask for somebody else's
    /// reset with your own Origin, and they receive a genuine Housing Hub email
    /// carrying their token to a site you control.
    /// </summary>
    [Fact]
    public void AnAttackersOrigin_IsIgnoredEntirely()
    {
        Assert.Equal(Production, Build(origin: "https://evil.example").Resolve());
    }

    [Fact]
    public void ALookalikeSuffixHost_IsIgnored()
    {
        Assert.Equal(Production, Build(origin: "https://housinghub.ng.evil.example").Resolve());
    }

    /// <summary>Referer carries a path, and none of it should reach the link.</summary>
    [Fact]
    public void WithNoOrigin_TheRefererIsUsedWithoutItsPath()
    {
        Assert.Equal(Preview, Build(referer: $"{Preview}/reset-password?x=1").Resolve());
    }

    /// <summary>Origin wins: it is set by the browser and carries no path.</summary>
    [Fact]
    public void OriginBeatsReferer()
    {
        Assert.Equal(
            Preview,
            Build(origin: Preview, referer: $"{Production}/anything").Resolve());
    }

    /// <summary>
    /// A mail sent from a background job has no request to read — expiry warnings and
    /// settlement notices are sent on a timer, so this is the normal path, not an
    /// error one.
    /// </summary>
    [Fact]
    public void WithNoRequestAtAll_TheConfiguredDefaultIsUsed()
    {
        Assert.Equal(Production, Build().Resolve());
    }

    [Fact]
    public void ATrailingSlashOnTheDefault_IsTrimmed()
    {
        Assert.Equal(Production, Build(configuration: Config($"{Production}/")).Resolve());
    }

    /// <summary>
    /// Nothing configured and nothing recognised still has to produce a usable link,
    /// and localhost is the only honest guess.
    /// </summary>
    [Fact]
    public void WithNothingConfigured_LocalhostIsTheFallback()
    {
        Assert.Equal("https://localhost", Build(configuration: Config(null)).Resolve());
    }
}
