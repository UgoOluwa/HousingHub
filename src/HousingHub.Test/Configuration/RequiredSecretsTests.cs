using HousingHub.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace HousingHub.Test.Configuration;

public class RequiredSecretsTests
{
    private const string ValidSigningKey = "a-genuinely-random-32-plus-character-value";

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Validate_AllValuesPresentAndValid_DoesNotThrow()
    {
        var config = BuildConfig(new()
        {
            ["Jwt:Secret"] = ValidSigningKey,
            ["Google:ClientId"] = "a-real-client-id",
        });

        var exception = Record.Exception(() =>
            RequiredSecrets.Validate(config, ["Jwt:Secret"], ["Google:ClientId"]));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MissingSigningKey_Throws()
    {
        var config = BuildConfig(new());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RequiredSecrets.Validate(config, ["Jwt:Secret"], []));

        Assert.Contains("Jwt:Secret", exception.Message);
        Assert.Contains("missing or is still a placeholder", exception.Message);
    }

    [Fact]
    public void Validate_SigningKeyShorterThanMinimumLength_Throws()
    {
        var config = BuildConfig(new() { ["Jwt:Secret"] = "tooshort" });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RequiredSecrets.Validate(config, ["Jwt:Secret"], []));

        Assert.Contains("Jwt:Secret", exception.Message);
        Assert.Contains("at least 32", exception.Message);
    }

    [Theory]
    [InlineData("your-worker-secret-replace-this-please")]
    [InlineData("CHANGEME-and-make-it-long-enough-too")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Validate_SigningKeyContainingPlaceholderMarker_ThrowsEvenWhenLongEnough(string placeholderValue)
    {
        Assert.True(placeholderValue.Length >= 32, "Test value must be long enough that only the placeholder check can fail it.");
        var config = BuildConfig(new() { ["Jwt:Secret"] = placeholderValue });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RequiredSecrets.Validate(config, ["Jwt:Secret"], []));

        Assert.Contains("Jwt:Secret", exception.Message);
    }

    [Fact]
    public void Validate_PlaceholderMatchingIsCaseInsensitive()
    {
        var config = BuildConfig(new() { ["Jwt:Secret"] = "TODO-set-a-real-value-before-deploying" });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RequiredSecrets.Validate(config, ["Jwt:Secret"], []));

        Assert.Contains("Jwt:Secret", exception.Message);
    }

    [Fact]
    public void Validate_MissingOtherRequiredKey_Throws()
    {
        var config = BuildConfig(new() { ["Jwt:Secret"] = ValidSigningKey });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RequiredSecrets.Validate(config, ["Jwt:Secret"], ["Resend:ApiKey"]));

        Assert.Contains("Resend:ApiKey", exception.Message);
    }

    [Fact]
    public void Validate_OtherRequiredKeyIsNotLengthChecked()
    {
        // "Other required" keys only need to be present and non-placeholder — no
        // minimum length applies, unlike signing keys.
        var config = BuildConfig(new() { ["Resend:ApiKey"] = "short" });

        var exception = Record.Exception(() =>
            RequiredSecrets.Validate(config, [], ["Resend:ApiKey"]));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceValue_TreatedAsMissing(string value)
    {
        var config = BuildConfig(new() { ["Jwt:Secret"] = value });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RequiredSecrets.Validate(config, ["Jwt:Secret"], []));

        Assert.Contains("Jwt:Secret", exception.Message);
    }

    [Fact]
    public void Validate_MultipleProblems_ListsEveryOffendingKeyInOneException()
    {
        // A misconfigured deployment should be fixable in one pass, not one
        // restart-and-read-the-log per missing value.
        var config = BuildConfig(new()
        {
            ["Jwt:Secret"] = "short",
            // Admin:Secret intentionally absent.
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RequiredSecrets.Validate(config, ["Jwt:Secret", "Admin:Secret"], ["Resend:ApiKey"]));

        Assert.Contains("Jwt:Secret", exception.Message);
        Assert.Contains("Admin:Secret", exception.Message);
        Assert.Contains("Resend:ApiKey", exception.Message);
    }
}
