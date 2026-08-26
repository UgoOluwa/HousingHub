using HousingHub.Core.Observability;
using Microsoft.Extensions.Configuration;
using Sentry;

namespace HousingHub.Test.Observability;

/// <summary>
/// These exist because of a production incident rather than a design: shipping
/// <c>options.Dsn = null</c> for a missing DSN took the admin API down on its first
/// boot in production. The SDK reads null as "not configured" and throws; only an
/// empty string disables it. Nothing in the suite covered the no-DSN path, which is
/// the path every environment uses until a DSN exists.
/// </summary>
public class SentryOptionsConfiguratorTests
{
    private const string RealDsn = "https://abc123@o123456.ingest.sentry.io/7654321";

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Configure_NoDsnConfigured_DisablesRatherThanThrowing()
    {
        var options = new SentryOptions();

        SentryOptionsConfigurator.Configure(options, BuildConfig(new()));

        // Empty, NOT null. Null is what caused hosting to fail to start.
        Assert.Equal(string.Empty, options.Dsn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Configure_BlankDsn_DisablesRatherThanThrowing(string? dsn)
    {
        var options = new SentryOptions();

        SentryOptionsConfigurator.Configure(options, BuildConfig(new() { ["Sentry:Dsn"] = dsn }));

        Assert.Equal(string.Empty, options.Dsn);
    }

    [Fact]
    public void Configure_RealDsn_IsPassedThrough()
    {
        var options = new SentryOptions();

        SentryOptionsConfigurator.Configure(options, BuildConfig(new() { ["Sentry:Dsn"] = RealDsn }));

        Assert.Equal(RealDsn, options.Dsn);
    }

    [Fact]
    public void Configure_NoEnvironmentConfigured_DefaultsToProduction()
    {
        var options = new SentryOptions();

        SentryOptionsConfigurator.Configure(options, BuildConfig(new()));

        Assert.Equal("production", options.Environment);
    }

    [Fact]
    public void Configure_EnvironmentConfigured_IsUsed()
    {
        var options = new SentryOptions();

        SentryOptionsConfigurator.Configure(options, BuildConfig(new() { ["Sentry:Environment"] = "development" }));

        Assert.Equal("development", options.Environment);
    }

    [Fact]
    public void Configure_NeverSendsPersonallyIdentifyingInformation()
    {
        var options = new SentryOptions();

        SentryOptionsConfigurator.Configure(options, BuildConfig(new()));

        // KYC submissions and login payloads pass through these APIs; PII and
        // request bodies must stay out of the error tracker regardless of DSN.
        Assert.False(options.SendDefaultPii);
    }

    [Fact]
    public void Configure_LeavesAFlushWindowForLambda()
    {
        var options = new SentryOptions();

        SentryOptionsConfigurator.Configure(options, BuildConfig(new()));

        // Lambda freezes the execution environment the moment a response is
        // returned. With no flush window the event queued during a failed request
        // is suspended in memory and lost.
        Assert.True(options.FlushTimeout > TimeSpan.Zero);
    }
}
