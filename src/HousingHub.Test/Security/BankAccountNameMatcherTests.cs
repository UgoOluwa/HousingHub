using HousingHub.Core.Security;

namespace HousingHub.Test.Security;

/// <summary>
/// The only thing standing between an owner and somebody else's rent.
/// </summary>
public class BankAccountNameMatcherTests
{
    [Theory]
    [InlineData("Bayo Eze", "BAYO EZE")]
    [InlineData("Bayo Eze", "bayo eze")]
    // Nigerian banks return names in no fixed order...
    [InlineData("Bayo Eze", "EZE BAYO")]
    // ...and often with middle names the account holder never gave us.
    [InlineData("Bayo Eze", "EZE BAYO CHUKWU")]
    [InlineData("Ada Obi", "OBI, ADA NGOZI")]
    [InlineData("Chidi Okafor-Nwosu", "CHIDI OKAFOR NWOSU")]
    [InlineData("Bàbátúndé Káyòdé", "BÀBÁTÚNDÉ KÁYÒDÉ")]
    public void ANameTheBankConfirms_Matches(string verified, string fromBank)
    {
        Assert.True(BankAccountNameMatcher.Matches(verified, fromBank));
    }

    /// <summary>An initial on either side is not a reason to refuse a real match.</summary>
    [Fact]
    public void AnInitialIsIgnored()
    {
        Assert.True(BankAccountNameMatcher.Matches("Bayo O Eze", "EZE BAYO"));
    }

    [Theory]
    [InlineData("Bayo Eze", "ADA OBI")]
    // A surname in common is not the same person.
    [InlineData("Bayo Eze", "NGOZI EZE")]
    // Nor is a first name.
    [InlineData("Bayo Eze", "BAYO OKAFOR")]
    // A partial word is not a name: "EZ" must not pass for "EZE".
    [InlineData("Bayo Eze", "BAYO EZ")]
    public void SomebodyElsesAccount_IsRefused(string verified, string fromBank)
    {
        Assert.False(BankAccountNameMatcher.Matches(verified, fromBank));
    }

    /// <summary>
    /// Nothing on file is a reason to stop, not a reason to trust whatever the bank
    /// said — an empty expectation would otherwise match every account there is.
    /// </summary>
    [Theory]
    [InlineData(null, "BAYO EZE")]
    [InlineData("", "BAYO EZE")]
    [InlineData("   ", "BAYO EZE")]
    [InlineData("B E", "BAYO EZE")]
    [InlineData("Bayo Eze", null)]
    [InlineData("Bayo Eze", "")]
    public void AMissingNameOnEitherSide_IsRefused(string? verified, string? fromBank)
    {
        Assert.False(BankAccountNameMatcher.Matches(verified, fromBank));
    }
}
