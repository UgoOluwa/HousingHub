using System.Security.Cryptography;
using System.Text;
using HousingHub.Service.Commons.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HousingHub.Test.Payments;

/// <summary>
/// The webhook signature check — the only thing standing between a stranger and a
/// free verification.
/// </summary>
/// <remarks>
/// The endpoint is anonymous by necessity: Paystack has no session with us. So this
/// check <i>is</i> the authentication, and a bug in it means anyone who can find
/// the URL can settle their own payments.
/// </remarks>
public class PaystackWebhookSignatureTests
{
    private const string SecretKey = "sk_test_not_a_real_key_0123456789";

    private static PaystackPaymentGateway CreateGateway(string? secretKey = SecretKey)
    {
        var settings = new Dictionary<string, string?>();
        if (secretKey is not null)
            settings[PaystackPaymentGateway.SecretKeyConfigKey] = secretKey;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new PaystackPaymentGateway(
            new HttpClient { BaseAddress = new Uri("https://api.paystack.co/") },
            configuration,
            NullLogger<PaystackPaymentGateway>.Instance);
    }

    /// <summary>
    /// Signs with the reference implementation rather than reusing ours, so the test
    /// verifies the choice of algorithm, key and encoding — not just that the code
    /// agrees with itself.
    /// </summary>
    private static string Sign(string body, string secretKey = SecretKey)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    private const string Body = """{"event":"charge.success","data":{"reference":"HH-abc","amount":750000}}""";

    [Fact]
    public void AGenuineSignature_IsAccepted()
    {
        Assert.True(CreateGateway().IsWebhookAuthentic(Body, Sign(Body)));
    }

    /// <summary>Paystack sends lower-case hex; accepting either costs nothing and avoids a brittle failure.</summary>
    [Fact]
    public void AGenuineSignature_IsAccepted_InUpperCaseHex()
    {
        Assert.True(CreateGateway().IsWebhookAuthentic(Body, Sign(Body).ToUpperInvariant()));
    }

    [Fact]
    public void ASignatureFromAnotherKey_IsRejected()
    {
        var forged = Sign(Body, "sk_test_attacker_key");

        Assert.False(CreateGateway().IsWebhookAuthentic(Body, forged));
    }

    /// <summary>
    /// The case that matters most: a valid signature over a body that was then
    /// edited — the shape of "same webhook, bigger amount".
    /// </summary>
    [Fact]
    public void AGenuineSignatureOverAnEditedBody_IsRejected()
    {
        var signature = Sign(Body);
        var tampered = Body.Replace("750000", "1");

        Assert.False(CreateGateway().IsWebhookAuthentic(tampered, signature));
    }

    [Fact]
    public void AMissingSignature_IsRejected()
    {
        var gateway = CreateGateway();

        Assert.False(gateway.IsWebhookAuthentic(Body, null));
        Assert.False(gateway.IsWebhookAuthentic(Body, ""));
        Assert.False(gateway.IsWebhookAuthentic(Body, "   "));
    }

    [Fact]
    public void AMalformedSignature_IsRejectedRatherThanThrowing()
    {
        var gateway = CreateGateway();

        Assert.False(gateway.IsWebhookAuthentic(Body, "not-hex-at-all"));
        Assert.False(gateway.IsWebhookAuthentic(Body, "zzzz"));
        // Right length, invalid characters.
        Assert.False(gateway.IsWebhookAuthentic(Body, new string('z', 128)));
    }

    /// <summary>
    /// A truncated signature that correctly prefixes the real one must not pass.
    /// This is the shape of a length-confusion bug.
    /// </summary>
    [Fact]
    public void ATruncatedButCorrectPrefix_IsRejected()
    {
        var signature = Sign(Body);

        Assert.False(CreateGateway().IsWebhookAuthentic(Body, signature[..64]));
        Assert.False(CreateGateway().IsWebhookAuthentic(Body, signature + "00"));
    }

    /// <summary>
    /// With no key configured nothing can be verified, so nothing may be accepted.
    /// Failing open here would mean a misconfigured environment settles anything it
    /// is sent.
    /// </summary>
    [Fact]
    public void WithNoSecretKeyConfigured_EverythingIsRejected()
    {
        var gateway = CreateGateway(secretKey: null);

        Assert.False(gateway.IsConfigured);
        Assert.False(gateway.IsWebhookAuthentic(Body, Sign(Body)));
    }

    [Fact]
    public void WithABlankSecretKey_EverythingIsRejected()
    {
        var gateway = CreateGateway(secretKey: "   ");

        Assert.False(gateway.IsConfigured);
        Assert.False(gateway.IsWebhookAuthentic(Body, Sign(Body)));
    }

    /// <summary>
    /// Whitespace-sensitive by design: the signature covers exact bytes, so any
    /// re-serialisation of the body breaks it. This documents why the controller
    /// reads the raw stream instead of binding a model.
    /// </summary>
    [Fact]
    public void ReformattedJson_IsRejected_EvenThoughItIsEquivalent()
    {
        var signature = Sign(Body);
        var reformatted = Body.Replace(",", ", ");

        Assert.False(CreateGateway().IsWebhookAuthentic(reformatted, signature));
    }
}
