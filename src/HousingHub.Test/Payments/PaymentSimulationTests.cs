using HousingHub.Service.Commons.Payments;

namespace HousingHub.Test.Payments;

/// <summary>
/// The gate on the only check standing between an owner and somebody else's rent.
/// </summary>
public class PaymentSimulationTests
{
    [Fact]
    public void OnATestKey_TheTestBankSimulates()
    {
        Assert.True(PaymentSimulation.IsSimulated(PaymentSimulation.TestBankCode, isTestMode: true));
    }

    /// <summary>
    /// "001" is a string anybody can send. Without the key check it would be a
    /// password for routing rent anywhere.
    /// </summary>
    [Fact]
    public void OnALiveKey_TheTestBankCodeSimulatesNothing()
    {
        Assert.False(PaymentSimulation.IsSimulated(PaymentSimulation.TestBankCode, isTestMode: false));
    }

    [Theory]
    [InlineData("058")]
    [InlineData("999992")]
    [InlineData("0001")]
    [InlineData("01")]
    [InlineData("1")]
    [InlineData(" 001")]
    [InlineData("001 ")]
    [InlineData(null)]
    [InlineData("")]
    public void NothingElseSimulates_EvenOnATestKey(string? bankCode)
    {
        Assert.False(PaymentSimulation.IsSimulated(bankCode, isTestMode: true));
    }
}
