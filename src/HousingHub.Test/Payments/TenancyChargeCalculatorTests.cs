using HousingHub.Service.Commons.Payments;
using Microsoft.Extensions.Configuration;

namespace HousingHub.Test.Payments;

/// <summary>
/// What a tenant pays, and how it divides.
/// </summary>
/// <remarks>
/// The owner's share and ours have to add up to the total exactly. The provider is
/// told what we keep and pays the owner the remainder, so a slip here is money
/// going to the wrong party rather than a wrong number on a screen.
/// </remarks>
public class TenancyChargeCalculatorTests
{
    private const long Rent = 5_000_000_00;   // ₦5,000,000
    private const long Fees = 500_000_00;     // ₦500,000

    private static TenancyChargeCalculator Build(decimal? commissionPercent = 5m)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Payments:Processing:Percent"] = "1.5",
            ["Payments:Processing:FlatKobo"] = "10000",
            ["Payments:Processing:CapKobo"] = "200000",
            ["Payments:Processing:FlatWaivedBelowKobo"] = "250000",
        };

        if (commissionPercent is not null)
            settings["Payments:Commission:Percent"] = commissionPercent.Value.ToString();

        return new TenancyChargeCalculator(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    [Fact]
    public void CommissionIsFivePercentOfTheRent()
    {
        Assert.True(Build().TryCalculate(Rent, Fees, out var charge, out _));

        // 5% of ₦5,000,000 — the rent, not the ₦5,500,000 total.
        Assert.Equal(250_000_00, charge.CommissionKobo);
    }

    /// <summary>
    /// The owner's own fees are their charges, collected on their behalf. Taking a
    /// share would mean our commission grew every time an owner added a line.
    /// </summary>
    [Fact]
    public void TheOwnersFeesAreNotCommissioned()
    {
        Build().TryCalculate(Rent, 0, out var withoutFees, out _);
        Build().TryCalculate(Rent, Fees, out var withFees, out _);

        Assert.Equal(withoutFees.CommissionKobo, withFees.CommissionKobo);
    }

    /// <summary>
    /// The invariant the split rests on: what the owner gets plus what we keep is
    /// exactly what the tenant was charged, with nothing unaccounted for.
    /// </summary>
    [Theory]
    [InlineData(5_000_000_00, 500_000_00)]
    [InlineData(1_200_000_00, 0)]
    [InlineData(150_000_00, 37_500_00)]
    [InlineData(1_00, 0)]
    public void TheSharesAlwaysAddUpToTheTotal(long rent, long fees)
    {
        Assert.True(Build().TryCalculate(rent, fees, out var charge, out _));

        Assert.Equal(charge.TotalKobo, charge.OwnerReceivesKobo + charge.PlatformKeepsKobo);
    }

    /// <summary>The owner is owed the rent and their fees, untouched by what we charge.</summary>
    [Fact]
    public void TheOwnerReceivesTheRentAndTheirFeesInFull()
    {
        Build().TryCalculate(Rent, Fees, out var charge, out _);

        Assert.Equal(Rent + Fees, charge.OwnerReceivesKobo);
    }

    /// <summary>
    /// The tenant bears the commission and the processing fee, which is what makes
    /// the total larger than the rent they agreed.
    /// </summary>
    [Fact]
    public void TheTenantPaysEverythingOnTop()
    {
        Build().TryCalculate(Rent, Fees, out var charge, out _);

        Assert.Equal(
            charge.RentKobo + charge.OwnerFeesKobo + charge.CommissionKobo + charge.ProcessingFeeKobo,
            charge.TotalKobo);
        Assert.True(charge.TotalKobo > Rent + Fees);
    }

    /// <summary>The provider caps its fee, and so must we — at ₦2,000.</summary>
    [Fact]
    public void TheProcessingFeeIsCapped()
    {
        Build().TryCalculate(Rent, Fees, out var charge, out _);

        Assert.Equal(200000, charge.ProcessingFeeKobo);
    }

    /// <summary>Below ₦2,500 the provider waives its flat part, so we do not charge it.</summary>
    [Fact]
    public void TheFlatPartIsWaivedOnASmallCharge()
    {
        Build().TryCalculate(1_000_00, 0, out var charge, out _);

        // 5% commission on ₦1,000 of rent is ₦50; 1.5% of ₦1,050 is ₦15.75 — and no
        // ₦100 flat part, because the charge is under ₦2,500.
        Assert.Equal(15_75, charge.ProcessingFeeKobo);
    }

    /// <summary>
    /// Rounded up, because rounding down under-charges by up to a kobo on every
    /// let in the one direction we cannot invoice for later.
    /// </summary>
    [Fact]
    public void CommissionRoundsInOurFavourRatherThanAway()
    {
        // 5% of ₦333.33 is ₦16.6665.
        Build().TryCalculate(333_33, 0, out var charge, out _);

        Assert.Equal(16_67, charge.CommissionKobo);
    }

    /// <summary>
    /// Unset is an error, not nought percent. Silence should not price a let at
    /// nothing — that is how a platform runs a month of lets for free and finds out
    /// from its bank statement.
    /// </summary>
    [Fact]
    public void NoConfiguredCommission_RefusesRatherThanChargingNothing()
    {
        Assert.False(Build(commissionPercent: null).TryCalculate(Rent, Fees, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void AZeroCommission_IsAlsoRefused()
    {
        Assert.False(Build(commissionPercent: 0m).TryCalculate(Rent, Fees, out _, out _));
    }

    /// <summary>A tenancy with no rent on it has nothing to charge for.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void NoRent_IsRefused(long rent)
    {
        Assert.False(Build().TryCalculate(rent, 0, out _, out var error));
        Assert.NotNull(error);
    }
}
