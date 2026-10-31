using Microsoft.Extensions.Configuration;

namespace HousingHub.Service.Commons.Payments;

/// <summary>
/// What a tenant pays, broken into the parts it is made of.
/// </summary>
/// <remarks>
/// Every figure is kobo. <see cref="OwnerReceivesKobo"/> and
/// <see cref="PlatformKeepsKobo"/> add up to <see cref="TotalKobo"/>, and that is
/// the invariant the split depends on — the provider is told what we keep and pays
/// the owner the remainder, so an arithmetic slip here is money going to the wrong
/// party rather than a wrong number on a screen.
/// </remarks>
public sealed record TenancyCharge(
    long RentKobo,
    long OwnerFeesKobo,
    long CommissionKobo,
    long ProcessingFeeKobo)
{
    /// <summary>What the tenant is asked for.</summary>
    public long TotalKobo => RentKobo + OwnerFeesKobo + CommissionKobo + ProcessingFeeKobo;

    /// <summary>Paid straight to the owner's account. Never held by us.</summary>
    public long OwnerReceivesKobo => RentKobo + OwnerFeesKobo;

    /// <summary>
    /// Our commission plus the provider's processing fee, which the provider
    /// deducts from this share.
    /// </summary>
    public long PlatformKeepsKobo => CommissionKobo + ProcessingFeeKobo;
}

/// <summary>
/// Works out the commission and the processing fee on a let.
/// </summary>
/// <remarks>
/// <para>
/// Both come out of configuration rather than code, because both are commercial
/// numbers that change without a deployment being the right way to change them.
/// An unset commission is an error, never a free let — the same rule the fee
/// catalogue follows, and for the same reason: silence should not price something
/// at zero.
/// </para>
/// <para>
/// The processing fee is added on top of what the owner is owed, so the tenant
/// bears it and the owner receives the rent in full. We tell the provider to take
/// its fee from our share, which is where the tenant's contribution to it already
/// sits.
/// </para>
/// </remarks>
public class TenancyChargeCalculator
{
    public const string CommissionPercentKey = "Payments:Commission:Percent";
    public const string ProcessingPercentKey = "Payments:Processing:Percent";
    public const string ProcessingFlatKoboKey = "Payments:Processing:FlatKobo";
    public const string ProcessingCapKoboKey = "Payments:Processing:CapKobo";
    public const string ProcessingFlatWaivedBelowKoboKey = "Payments:Processing:FlatWaivedBelowKobo";

    private readonly IConfiguration _configuration;

    public TenancyChargeCalculator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Breaks a let down into what each party gets.
    /// </summary>
    /// <param name="error">Why no charge could be worked out. Null on success.</param>
    /// <remarks>
    /// Commission is a share of <paramref name="rentKobo"/> only. The owner's own
    /// fees pass through untouched.
    /// </remarks>
    public bool TryCalculate(long rentKobo, long ownerFeesKobo, out TenancyCharge charge, out string? error)
    {
        charge = new TenancyCharge(0, 0, 0, 0);

        if (rentKobo <= 0)
        {
            error = "This tenancy has no rent set on it, so there is nothing to pay.";
            return false;
        }

        var percent = _configuration.GetValue<decimal?>(CommissionPercentKey);

        // Unset is an error, not nought percent. A missing setting means nobody has
        // decided what we charge, and letting that read as "free" is how a platform
        // runs a month of lets for nothing and finds out from its bank statement.
        if (percent is null or <= 0)
        {
            error = "Commission has not been configured on this environment, so no payment can be taken.";
            return false;
        }

        var ownerReceives = rentKobo + ownerFeesKobo;

        // On the rent alone, not on the rent plus whatever the owner added. The
        // owner's fees are their charges, collected on their behalf — an agency fee
        // or a caution deposit is not income we had any part in, and taking a share
        // of it would mean our commission grew every time an owner added a line.
        //
        // Rounded up: the alternative is systematically under-charging by up to a
        // kobo on every let, in the one direction we cannot invoice for later.
        var commission = (long)Math.Ceiling(rentKobo * percent.Value / 100m);

        charge = new TenancyCharge(
            rentKobo, ownerFeesKobo, commission, ProcessingFeeKobo(ownerReceives + commission));

        error = null;
        return true;
    }

    /// <summary>
    /// The provider's cut, computed on what it will actually charge against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Strictly this is circular — the provider charges a percentage of the total,
    /// and the fee is part of the total — so adding the fee to the amount raises the
    /// fee slightly. We take the fee on the pre-fee figure and absorb the few naira
    /// of difference rather than solving for it, because the alternative is a total
    /// that cannot be explained to the person paying it.
    /// </para>
    /// <para>
    /// Defaults match Paystack's published NGN pricing at the time of writing:
    /// 1.5% plus ₦100, the flat part waived under ₦2,500, the whole thing capped at
    /// ₦2,000. They are configuration because they are the provider's prices, not
    /// ours.
    /// </para>
    /// </remarks>
    private long ProcessingFeeKobo(long amountKobo)
    {
        var percent = _configuration.GetValue<decimal?>(ProcessingPercentKey) ?? 1.5m;
        var flat = _configuration.GetValue<long?>(ProcessingFlatKoboKey) ?? 100_00;
        var cap = _configuration.GetValue<long?>(ProcessingCapKoboKey) ?? 2_000_00;
        var waivedBelow = _configuration.GetValue<long?>(ProcessingFlatWaivedBelowKoboKey) ?? 2_500_00;

        var fee = (long)Math.Ceiling(amountKobo * percent / 100m);
        if (amountKobo >= waivedBelow) fee += flat;

        return cap > 0 ? Math.Min(fee, cap) : fee;
    }
}
