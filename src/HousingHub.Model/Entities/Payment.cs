using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

/// <summary>
/// One attempt to collect a fee owed to Housing Hub.
/// </summary>
/// <remarks>
/// <para>
/// Fees only — never custody of somebody else's money. Holding a renter's first
/// rent, even for 48 hours, is escrow, which under CBN rules is Mobile Money
/// Operator territory and must run on a licensed partner's product rather than on
/// this table. See docs/transaction-lifecycle-plan.md, part 1.4.
/// </para>
/// <para>
/// <b>An attempt, not an invoice.</b> A payer who abandons the gateway and comes
/// back gets a second row, and settling either one is a real payment. That is why
/// nothing here is keyed on "the payment for case X" — the question the code asks
/// is "is there a settled payment for case X", which several rows may answer.
/// </para>
/// </remarks>
[DynamoDBTable("Payments")]
public class Payment : BaseEntity
{
    /// <summary>
    /// Our reference, and the one given to the gateway. Unique per attempt.
    /// </summary>
    /// <remarks>
    /// Ours rather than the gateway's, because it has to exist before the gateway
    /// has been called — a webhook that arrives while the initialise response is
    /// still in flight has to find a row to land on.
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("Reference-index")]
    public string Reference { get; set; } = null!;

    [DynamoDBGlobalSecondaryIndexHashKey("CustomerId-index")]
    public Guid CustomerId { get; set; }

    public PaymentPurpose Purpose { get; set; }

    /// <summary>
    /// What this pays for — currently a <see cref="VerificationCase"/> id.
    /// </summary>
    /// <remarks>
    /// Null only for a standalone identity purchase, which buys a fact about the
    /// payer rather than a decision on a specific subject.
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("SubjectId-index")]
    public Guid? SubjectId { get; set; }

    /// <summary>Total asked for, in kobo.</summary>
    /// <remarks>
    /// <para>
    /// Kobo, as a whole number, everywhere. Not naira, and not a decimal: Paystack's
    /// API takes an integer of the minor unit, and every currency bug worth having
    /// starts with a rounding step between the price shown and the amount charged.
    /// The only conversion happens at the point of display.
    /// </para>
    /// <para>
    /// Computed server-side from the fee catalogue. A client never sends an amount.
    /// </para>
    /// </remarks>
    public long AmountKobo { get; set; }

    /// <summary>The <see cref="Purpose"/> fee's share of <see cref="AmountKobo"/>.</summary>
    public long PurposeFeeKobo { get; set; }

    /// <summary>
    /// The bundled identity check's share of <see cref="AmountKobo"/>, or 0 when the
    /// payer already holds identity verification.
    /// </summary>
    /// <remarks>
    /// One rule on both sides of the marketplace: identity is bundled into the first
    /// paid verification you need, and never charged again. Kept as its own figure
    /// rather than folded into the total so a receipt can say what was actually
    /// bought, and so a payer who has been charged once can see they were not
    /// charged twice.
    /// </remarks>
    public long IdentityFeeKobo { get; set; }

    /// <summary>ISO currency code. NGN throughout; stored so a receipt is unambiguous.</summary>
    public string Currency { get; set; } = "NGN";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>The gateway's own identifier, once it has given us one.</summary>
    public string? ProviderReference { get; set; }

    /// <summary>Which provider this attempt went to, so a later migration can tell them apart.</summary>
    public string? Provider { get; set; }

    /// <summary>How it was paid — card, bank transfer, USSD. Reported by the gateway.</summary>
    public string? Channel { get; set; }

    /// <summary>Where to send the payer to complete this attempt.</summary>
    /// <remarks>
    /// Stored so a second initialise for the same case can hand back the attempt
    /// already in flight rather than starting another one, which is the difference
    /// between a double-clicked button and a double charge.
    /// </remarks>
    public string? AuthorisationUrl { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>Why this was flagged for a person to look at. Set with <see cref="PaymentStatus.Flagged"/>.</summary>
    public string? FlagNote { get; set; }

    /// <summary>Attribute value written for a flagged payment. Absent otherwise.</summary>
    public const string FlaggedMarker = "FLAGGED";

    /// <summary>
    /// Index key mirroring <see cref="PaymentStatus.Flagged"/>, so "payments needing
    /// a human" is a Query rather than a scan of every payment ever taken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sparse by design, the same shape as <c>VerificationCase.ReviewQueueStatus</c>:
    /// only flagged payments carry the attribute, so DynamoDB's index holds exactly
    /// the rows an admin is looking for and does not grow with successful ones.
    /// </para>
    /// <para>
    /// <b>The setter discards its argument.</b> The value derives from
    /// <see cref="Status"/>, which is the single source of truth; a setter exists
    /// only because the object mapper needs one to deserialize. Storing what comes
    /// back would let the two disagree, and would depend on the order the mapper
    /// happens to assign properties in — which is not specified.
    /// </para>
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("FlagWatch-index")]
    public string? FlagWatch
    {
        get => Status == PaymentStatus.Flagged ? FlaggedMarker : null;
        set { /* derived from Status — see remarks */ }
    }

    /// <summary>True once the gateway has confirmed the money.</summary>
    [DynamoDBIgnore]
    public bool IsSettled => Status == PaymentStatus.Successful;

    /// <summary>True when an identity check was bundled into this payment.</summary>
    [DynamoDBIgnore]
    public bool IncludesIdentityVerification => IdentityFeeKobo > 0;

    public Payment() { }

    public Payment(
        string reference,
        Guid customerId,
        PaymentPurpose purpose,
        Guid? subjectId,
        long purposeFeeKobo,
        long identityFeeKobo,
        string currency,
        string provider)
    {
        Id = Guid.NewGuid();
        Reference = reference;
        CustomerId = customerId;
        Purpose = purpose;
        SubjectId = subjectId;
        PurposeFeeKobo = purposeFeeKobo;
        IdentityFeeKobo = identityFeeKobo;
        AmountKobo = purposeFeeKobo + identityFeeKobo;
        Currency = currency;
        Provider = provider;
        Status = PaymentStatus.Pending;
        IsActive = true;
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a gateway-confirmed payment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The idempotency boundary.</b> Only a <see cref="PaymentStatus.Pending"/>
    /// payment can be settled, so a webhook delivered five times settles once and
    /// answers <see cref="PaymentSettlementOutcome.AlreadySettled"/> four times.
    /// Gateways retry on any non-2xx response and Paystack replays on request, so
    /// this will happen in normal operation, not just under attack — and it is also
    /// what makes a captured-and-replayed webhook body inert.
    /// </para>
    /// <para>
    /// <b>The amount is checked, not accepted.</b> A confirmed payment for the wrong
    /// amount hands over nothing and goes to <see cref="PaymentStatus.Flagged"/>.
    /// Settling on the gateway's figure would mean a payer who altered the amount
    /// gets what they asked for at whatever price they chose.
    /// </para>
    /// </remarks>
    /// <param name="amountPaidKobo">Amount the gateway confirms was paid, in kobo.</param>
    public PaymentSettlementOutcome TrySettle(long amountPaidKobo, string? providerReference, string? channel)
    {
        if (Status == PaymentStatus.Successful)
            return PaymentSettlementOutcome.AlreadySettled;

        if (Status != PaymentStatus.Pending)
            return PaymentSettlementOutcome.NotPending;

        if (amountPaidKobo != AmountKobo)
        {
            Status = PaymentStatus.Flagged;
            FlagNote =
                $"Gateway confirmed {amountPaidKobo} kobo against an expected {AmountKobo} kobo. " +
                "Nothing has been handed over.";
            DateModified = DateTime.UtcNow;
            return PaymentSettlementOutcome.AmountMismatch;
        }

        Status = PaymentStatus.Successful;
        ProviderReference = providerReference ?? ProviderReference;
        Channel = channel ?? Channel;
        PaidAt = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        return PaymentSettlementOutcome.Settled;
    }

    /// <summary>
    /// Records that the attempt did not result in a payment.
    /// </summary>
    /// <remarks>
    /// Only from Pending. A settled payment is never walked back by a later event —
    /// gateway events are not guaranteed to arrive in order, and an out-of-order
    /// failure for an earlier stage of a successful charge would otherwise revoke
    /// something already paid for.
    /// </remarks>
    public bool TryFail(PaymentStatus status, string? reason)
    {
        if (Status != PaymentStatus.Pending) return false;
        if (status is not (PaymentStatus.Failed or PaymentStatus.Abandoned)) return false;

        Status = status;
        FailureReason = reason;
        DateModified = DateTime.UtcNow;
        return true;
    }

    public void RecordInitialisation(string? authorisationUrl, string? providerReference)
    {
        AuthorisationUrl = authorisationUrl;
        ProviderReference = providerReference;
        DateModified = DateTime.UtcNow;
    }
}
