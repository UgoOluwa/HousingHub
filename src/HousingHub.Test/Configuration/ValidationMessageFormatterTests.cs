using HousingHub.Core.CustomResponses;

namespace HousingHub.Test.Configuration;

/// <summary>
/// These guard user-facing copy, which is why they assert on exact strings rather
/// than on shape. The formatter exists because model-state failures bypassed the
/// app's error envelope entirely: a KYC submission missing a phone number showed
/// "Request failed with status code 400", and an unselected property type showed
/// the raw {"PropertyType":["The value '0' is invalid."]}.
/// </summary>
public class ValidationMessageFormatterTests
{
    private static KeyValuePair<string, string[]> Failure(string field, params string[] errors) =>
        new(field, errors);

    // ── Humanise ─────────────────────────────────────────────

    [Theory]
    [InlineData("PhoneNumber", "phone number")]
    [InlineData("FirstName", "first name")]
    [InlineData("CompanyName", "company name")]
    [InlineData("PropertyType", "property type")]
    [InlineData("Email", "email")]
    public void Humanise_SplitsPascalCase(string field, string expected)
    {
        Assert.Equal(expected, ValidationMessageFormatter.Humanise(field));
    }

    [Theory]
    [InlineData("NationalIdNumber", "ID number")]
    [InlineData("DateOfBirth", "date of birth")]
    [InlineData("CacNumber", "CAC number")]
    public void Humanise_UsesOverridesWhereSplittingReadsBadly(string field, string expected)
    {
        Assert.Equal(expected, ValidationMessageFormatter.Humanise(field));
    }

    [Theory]
    [InlineData("Address.City", "city")]
    [InlineData("Files[0].Name", "name")]
    public void Humanise_TakesTheLeafOfAModelStatePath(string field, string expected)
    {
        // Model state keys are paths. "Address.City" is not a thing to say to a user.
        Assert.Equal(expected, ValidationMessageFormatter.Humanise(field));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Humanise_EmptyFieldNameStillReadsAsEnglish(string? field)
    {
        Assert.Equal("details", ValidationMessageFormatter.Humanise(field!));
    }

    // ── Summarise ────────────────────────────────────────────

    [Fact]
    public void Summarise_SingleMissingField_AsksForIt()
    {
        var message = ValidationMessageFormatter.Summarise(
            [Failure("PhoneNumber", "The PhoneNumber field is required.")]);

        Assert.Equal("Please add your phone number.", message);
    }

    [Fact]
    public void Summarise_SingleInvalidValue_DoesNotClaimItIsMissing()
    {
        // This is the PropertyType case: a value was supplied and did not fit. Telling
        // the user to "add" it would be wrong.
        var message = ValidationMessageFormatter.Summarise(
            [Failure("PropertyType", "The value '0' is invalid.")]);

        Assert.Equal("That property type doesn't look right. Please check it and try again.", message);
    }

    [Fact]
    public void Summarise_SeveralFields_NamesThemAll()
    {
        var message = ValidationMessageFormatter.Summarise(
        [
            Failure("PhoneNumber", "The PhoneNumber field is required."),
            Failure("DateOfBirth", "The DateOfBirth field is required."),
        ]);

        Assert.Equal("Please check these details: phone number, date of birth.", message);
    }

    [Fact]
    public void Summarise_NoFailures_StillReturnsSomethingSayable()
    {
        var message = ValidationMessageFormatter.Summarise([]);

        Assert.Equal("Please check the details you entered and try again.", message);
    }

    [Fact]
    public void Summarise_NeverLeaksACSharpPropertyName()
    {
        var message = ValidationMessageFormatter.Summarise(
            [Failure("PhoneNumber", "The PhoneNumber field is required.")]);

        Assert.DoesNotContain("PhoneNumber", message);
    }

    // ── DescribeEach ─────────────────────────────────────────

    [Fact]
    public void DescribeEach_DistinguishesMissingFromInvalid()
    {
        var described = ValidationMessageFormatter.DescribeEach(
        [
            Failure("PhoneNumber", "The PhoneNumber field is required."),
            Failure("PropertyType", "The value '0' is invalid."),
        ]);

        Assert.Equal(["Phone number is required.", "Property type isn't valid."], described);
    }

    [Fact]
    public void DescribeEach_TreatsFluentValidationsEmptyWordingAsMissing()
    {
        // NotEmpty() renders "'Phone Number' must not be empty." — the same condition
        // in different words, and it should not read as "isn't valid".
        var described = ValidationMessageFormatter.DescribeEach(
            [Failure("PhoneNumber", "'Phone Number' must not be empty.")]);

        Assert.Equal(["Phone number is required."], described);
    }
}
