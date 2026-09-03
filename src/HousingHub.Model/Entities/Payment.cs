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
        // Refunding a flagged payment resolves it, so the marker goes with the
        // status and the row leaves the queue. Keeping it would leave an admin
        // looking at work they have already done.
        get => Status == PaymentStatus.Flagged ? FlaggedMarker : null;
        set { /* derived from Status — see remarks */ }
    }

    // ── Refunds ─────────────────────────────────────────────────

    /// <summary>Why the money was sent back. Required — a refund with no reason is unauditable.</summary>
    public string? RefundReason { get; set; }

    /// <summary>Which admin asked for it.</summary>
    /// <remarks>
    /// Recorded because this is the only action in the system that moves money out.
    /// "Who refunded this and why" must be answerable from the row itself, not from
    /// a log that rotates.
    /// </remarks>
    public Guid? RefundedByAdminId { get; set; }

    public DateTime? RefundRequestedAt { get; set; }
    public DateTime? RefundedAt { get; set; }

    /// <summary>
    /// What was actually sent back, in kobo.
    /// </summary>
    /// <remarks>
    /// Not assumed to equal <see cref="AmountKobo"/>. A flagged payment is flagged
    /// precisely because the confirmed amount differed from the amount asked for, and
    /// refunding what we asked for rather than what arrived would send back the wrong
    /// figure in the one case where it is most likely to matter.
    /// </remarks>
    public long? RefundAmountKobo { get; set; }

    /// <summary>The provider's identifier for the refund itself, distinct from the charge.</summary>
    public string? ProviderRefundReference { get; set; }

    /// <summary>True once the gateway has confirmed the money.</summary>
    /// <remarks>
    /// Deliberately narrow: only <see cref="PaymentStatus.Successful"/>. A refunded
    /// payment is not settled, so the verification gate stops accepting it without
    /// needing to know that refunds exist.
    /// </remarks>
    [DynamoDBIgnore]
    public bool IsSettled => Status == PaymentStatus.Successful;

    /// <summary>
    /// True when this payment could be sent back.
    /// </summary>
    /// <remarks>
    /// A flagged payment is included, and is usually the reason to refund at all —
    /// money arrived, nothing was handed over, and returning it is the honest
    /// resolution. A pending or failed payment has nothing to return.
    /// </remarks>
    [DynamoDBIgnore]
    public bool IsRefundable =>
        Status is PaymentStatus.Successful or PaymentStatus.Flagged;

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

    /// <summary>
    /// Records that a refund has been asked of the provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called <b>before</b> the provider is contacted, and that ordering is the point.
    /// If it were called after, two admins clicking at once — or one admin clicking
    /// twice — would each see a refundable payment and each send a refund. Moving to
    /// <see cref="PaymentStatus.RefundPending"/> first means the second attempt finds
    /// a refund already in flight.
    /// </para>
    /// <para>
    /// The cost of that ordering is the opposite failure: if the provider call then
    /// fails, the payment is left pending a refund that was never asked for. That is
    /// recoverable — see <see cref="TryAbandonRefund"/> — and it is the safer of the
    /// two, because the recoverable state is the one where no money moved twice.
    /// </para>
    /// </remarks>
    public RefundOutcome TryBeginRefund(long amountKobo, string reason, Guid adminId)
    {
        if (Status is PaymentStatus.RefundPending or PaymentStatus.Refunded)
            return RefundOutcome.AlreadyInProgress;

        if (!IsRefundable)
            return RefundOutcome.NotRefundable;

        Status = PaymentStatus.RefundPending;
        RefundAmountKobo = amountKobo;
        RefundReason = reason;
        RefundedByAdminId = adminId;
        RefundRequestedAt = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        return RefundOutcome.Requested;
    }

    /// <summary>
    /// Records the provider's confirmation that the money went back.
    /// </summary>
    /// <remarks>
    /// Idempotent for the same reason settlement is: refund webhooks are retried,
    /// and a second delivery must not restate the refund date or overwrite the
    /// figure with a later event's.
    /// </remarks>
    public RefundOutcome TryCompleteRefund(long? amountKobo, string? providerRefundReference)
    {
        if (Status == PaymentStatus.Refunded)
            return RefundOutcome.AlreadyInProgress;

        // Allowed from Successful and Flagged as well as RefundPending: a refund
        // issued directly in the provider's dashboard reaches us as a webhook
        // without ever having passed through this application.
        if (Status is not (PaymentStatus.RefundPending or PaymentStatus.Successful or PaymentStatus.Flagged))
            return RefundOutcome.NotRefundable;

        Status = PaymentStatus.Refunded;
        RefundAmountKobo = amountKobo ?? RefundAmountKobo;
        ProviderRefundReference = providerRefundReference ?? ProviderRefundReference;
        RefundedAt = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        return RefundOutcome.Completed;
    }

    /// <summary>
    /// Releases a refund claim the provider refused outright.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a <b>synchronous</b> refusal only — the provider declined the request
    /// there and then, no money moved, and the admin who asked is looking at the
    /// error. Nothing is owed and nothing needs queueing, so the payment goes back
    /// to what it was and the attempt is forgotten.
    /// </para>
    /// <para>
    /// Restores <see cref="PaymentStatus.Flagged"/> rather than
    /// <see cref="PaymentStatus.Successful"/> when there is already a flag note, so
    /// a flagged payment whose refund was refused returns to the queue instead of
    /// quietly reading as a normal completed payment.
    /// </para>
    /// <para>
    /// A refund that failed <i>later</i>, asynchronously, is a different thing
    /// entirely — see <see cref="TryFlagFailedRefund"/>.
    /// </para>
    /// </remarks>
    public bool TryAbandonRefund(string? failureReason)
    {
        if (Status != PaymentStatus.RefundPending) return false;

        Status = FlagNote is null ? PaymentStatus.Successful : PaymentStatus.Flagged;
        RefundRequestedAt = null;
        RefundAmountKobo = null;
        FailureReason = failureReason ?? FailureReason;
        DateModified = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Records that a refund the provider accepted has since failed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always flags.</b> This arrives by webhook, minutes or hours after an admin
    /// pressed the button and stopped watching, and it means a specific thing: we
    /// told somebody their money was coming back and it did not. Nobody finds that
    /// out unless it is put somewhere a person looks.
    /// </para>
    /// <para>
    /// <see cref="PaymentStatus.Flagged"/> rather than a new state, because the
    /// existing one already means exactly this — money is involved, code cannot
    /// resolve it, and a human has to. It also puts the row straight into the admin
    /// queue through <see cref="FlagWatch"/>, and leaves it refundable so the
    /// obvious next action is available.
    /// </para>
    /// <para>
    /// The attempt is deliberately <i>not</i> cleared the way
    /// <see cref="TryAbandonRefund"/> clears it. Who asked, why, when, and for how
    /// much are the evidence for whoever picks this up, and losing them would leave
    /// a flagged payment with no explanation of how it got that way.
    /// </para>
    /// </remarks>
    public bool TryFlagFailedRefund(string? providerReason)
    {
        if (Status != PaymentStatus.RefundPending) return false;

        long owedKobo = RefundAmountKobo ?? AmountKobo;

        Status = PaymentStatus.Flagged;
        FailureReason = providerReason ?? FailureReason;
        FlagNote =
            $"A refund of {owedKobo} kobo was accepted by the provider and then failed. "
            + "The payer has NOT had their money back. Check the transaction with the provider and retry the refund.";
        DateModified = DateTime.UtcNow;
        return true;
    }
}
