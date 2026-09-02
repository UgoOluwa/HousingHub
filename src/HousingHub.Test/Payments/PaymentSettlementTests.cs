using HousingHub.Model.Entities;
using HousingHub.Model.Enums;

namespace HousingHub.Test.Payments;

/// <summary>
/// The state machine that decides whether somebody gets what they paid for.
/// </summary>
/// <remarks>
/// Every test here is a way a payment integration loses money or gives something
/// away, and none of them can be arranged against a provider sandbox — you cannot
/// ask Paystack to confirm the wrong amount, or to deliver the same webhook twice
/// on demand.
/// </remarks>
public class PaymentSettlementTests
{
    private const long PurposeFee = 500_000;   // ₦5,000 in kobo
    private const long IdentityFee = 250_000;  // ₦2,500 in kobo

    private static Payment CreatePayment(long purposeFee = PurposeFee, long identityFee = IdentityFee) =>
        new(
            reference: "HH-test",
            customerId: Guid.NewGuid(),
            purpose: PaymentPurpose.BusinessVerification,
            subjectId: Guid.NewGuid(),
            purposeFeeKobo: purposeFee,
            identityFeeKobo: identityFee,
            currency: "NGN",
            provider: "paystack");

    [Fact]
    public void Constructor_TotalsTheBundle()
    {
        var payment = CreatePayment();

        Assert.Equal(PurposeFee + IdentityFee, payment.AmountKobo);
        Assert.True(payment.IncludesIdentityVerification);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.False(payment.IsSettled);
    }

    [Fact]
    public void IncludesIdentityVerification_IsFalse_WhenIdentityIsAlreadyHeld()
    {
        var payment = CreatePayment(identityFee: 0);

        Assert.False(payment.IncludesIdentityVerification);
        Assert.Equal(PurposeFee, payment.AmountKobo);
    }

    [Fact]
    public void TrySettle_ForTheFullAmount_Settles()
    {
        var payment = CreatePayment();

        var outcome = payment.TrySettle(payment.AmountKobo, "pstk_1", "card");

        Assert.Equal(PaymentSettlementOutcome.Settled, outcome);
        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.True(payment.IsSettled);
        Assert.Equal("pstk_1", payment.ProviderReference);
        Assert.Equal("card", payment.Channel);
        Assert.NotNull(payment.PaidAt);
    }

    /// <summary>
    /// The idempotency guarantee. Providers retry webhooks on any non-2xx response
    /// and Paystack replays them on request, so a repeat delivery is normal traffic
    /// — and a replayed capture of a valid body is the attack it also defends.
    /// </summary>
    [Fact]
    public void TrySettle_Twice_SettlesOnce()
    {
        var payment = CreatePayment();

        var first = payment.TrySettle(payment.AmountKobo, "pstk_1", "card");
        var settledAt = payment.PaidAt;

        var second = payment.TrySettle(payment.AmountKobo, "pstk_2", "bank_transfer");

        Assert.Equal(PaymentSettlementOutcome.Settled, first);
        Assert.Equal(PaymentSettlementOutcome.AlreadySettled, second);

        // Nothing about the settlement moved on the second delivery.
        Assert.Equal(settledAt, payment.PaidAt);
        Assert.Equal("pstk_1", payment.ProviderReference);
        Assert.Equal("card", payment.Channel);
    }

    /// <summary>
    /// A confirmed payment for less than we asked hands over nothing. Settling on
    /// the gateway's figure would let a payer choose the price.
    /// </summary>
    [Fact]
    public void TrySettle_ForLessThanAsked_FlagsAndDoesNotSettle()
    {
        var payment = CreatePayment();

        var outcome = payment.TrySettle(100, "pstk_1", "card");

        Assert.Equal(PaymentSettlementOutcome.AmountMismatch, outcome);
        Assert.Equal(PaymentStatus.Flagged, payment.Status);
        Assert.False(payment.IsSettled);
        Assert.Null(payment.PaidAt);
        Assert.NotNull(payment.FlagNote);
    }

    /// <summary>Overpayment is a mismatch too — it means the amount was not ours.</summary>
    [Fact]
    public void TrySettle_ForMoreThanAsked_Flags()
    {
        var payment = CreatePayment();

        var outcome = payment.TrySettle(payment.AmountKobo + 1, "pstk_1", "card");

        Assert.Equal(PaymentSettlementOutcome.AmountMismatch, outcome);
        Assert.Equal(PaymentStatus.Flagged, payment.Status);
    }

    /// <summary>Flagged is terminal. A later webhook must not quietly settle it.</summary>
    [Fact]
    public void TrySettle_OnAFlaggedPayment_IsRefused()
    {
        var payment = CreatePayment();
        payment.TrySettle(100, "pstk_1", "card");

        var outcome = payment.TrySettle(payment.AmountKobo, "pstk_2", "card");

        Assert.Equal(PaymentSettlementOutcome.NotPending, outcome);
        Assert.Equal(PaymentStatus.Flagged, payment.Status);
        Assert.False(payment.IsSettled);
    }

    [Fact]
    public void TrySettle_OnAFailedPayment_IsRefused()
    {
        var payment = CreatePayment();
        payment.TryFail(PaymentStatus.Failed, "card declined");

        var outcome = payment.TrySettle(payment.AmountKobo, "pstk_1", "card");

        Assert.Equal(PaymentSettlementOutcome.NotPending, outcome);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
    }

    /// <summary>
    /// Gateway events are not ordered. A failure event for an earlier stage of a
    /// charge that ultimately succeeded must not revoke the settlement.
    /// </summary>
    [Fact]
    public void TryFail_AfterSettlement_IsRefused()
    {
        var payment = CreatePayment();
        payment.TrySettle(payment.AmountKobo, "pstk_1", "card");

        var failed = payment.TryFail(PaymentStatus.Failed, "late failure event");

        Assert.False(failed);
        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.True(payment.IsSettled);
    }

    [Fact]
    public void TryFail_RefusesAStatusThatIsNotAFailure()
    {
        var payment = CreatePayment();

        Assert.False(payment.TryFail(PaymentStatus.Successful, null));
        Assert.False(payment.TryFail(PaymentStatus.Flagged, null));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void TryFail_RecordsAbandonment()
    {
        var payment = CreatePayment();

        Assert.True(payment.TryFail(PaymentStatus.Abandoned, "left the page"));
        Assert.Equal(PaymentStatus.Abandoned, payment.Status);
        Assert.Equal("left the page", payment.FailureReason);
    }

    // ── The sparse flagged index ─────────────────────────────────

    /// <summary>
    /// Only a flagged payment enters the admin queue's index. A pending or
    /// successful one carrying the marker would put every payment ever taken into
    /// an index meant to hold the handful needing a person.
    /// </summary>
    [Fact]
    public void FlagWatch_IsSetOnlyWhileFlagged()
    {
        var payment = CreatePayment();
        Assert.Null(payment.FlagWatch);

        payment.TrySettle(100, "pstk_1", "card");

        Assert.Equal(PaymentStatus.Flagged, payment.Status);
        Assert.Equal(Payment.FlaggedMarker, payment.FlagWatch);
    }

    [Fact]
    public void FlagWatch_IsAbsentOnASettledPayment()
    {
        var payment = CreatePayment();
        payment.TrySettle(payment.AmountKobo, "pstk_1", "card");

        Assert.Null(payment.FlagWatch);
    }

    /// <summary>
    /// The setter discards its argument, so the marker cannot disagree with the
    /// status it is derived from — including when the object mapper assigns
    /// properties in an order nothing specifies.
    /// </summary>
    [Fact]
    public void FlagWatch_CannotBeSetIndependentlyOfStatus()
    {
        var payment = CreatePayment();
        payment.FlagWatch = Payment.FlaggedMarker;

        Assert.Null(payment.FlagWatch);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }
}
