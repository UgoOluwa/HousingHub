using HousingHub.Service.Commons.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HousingHub.Test.Payments;

/// <summary>
/// How much of the provider's reasoning reaches the caller.
/// </summary>
/// <remarks>
/// A developer on a test key needs it; an owner on a live one is not the audience
/// for a message written about an API request.
/// </remarks>
public class GatewayExplanationTests
{
    private static PaystackPaymentGateway Build(string? secretKey)
    {
        var settings = new Dictionary<string, string?>();
        if (secretKey is not null) settings["Payments:Paystack:SecretKey"] = secretKey;

        return new PaystackPaymentGateway(
            new HttpClient { BaseAddress = new Uri("https://api.paystack.co/") },
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            NullLogger<PaystackPaymentGateway>.Instance);
    }

    [Fact]
    public void ATestKeyIsTestMode()
    {
        Assert.True(Build("sk_test_abc123").IsTestMode);
    }

    [Fact]
    public void ALiveKeyIsNot()
    {
        Assert.False(Build("sk_live_abc123").IsTestMode);
    }

    /// <summary>
    /// No key is not test mode. It is no mode at all, and reading it as test would
    /// mean a misconfigured production environment quietly relaxed its checks.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnUnsetKeyIsNotTestMode(string? secretKey)
    {
        Assert.False(Build(secretKey).IsTestMode);
    }

    /// <summary>A key that merely mentions the word is not a test key.</summary>
    [Theory]
    [InlineData("sk_live_test_abc")]
    [InlineData("test_sk_abc")]
    [InlineData("SK_TEST_ABC")]
    public void OnlyTheRealPrefixCounts(string secretKey)
    {
        Assert.False(Build(secretKey).IsTestMode);
    }
}
