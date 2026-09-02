namespace HousingHub.Model.Enums;

/// <summary>
/// What a payment was for.
/// </summary>
/// <remarks>
/// <para>
/// The <i>primary</i> thing bought. Identity verification is normally bundled into
/// whichever of the others the payer reaches first rather than sold on its own —
/// see <see cref="Entities.Payment.IdentityFeeKobo"/> — but it is also a purpose in
/// its own right, because docs/transaction-lifecycle-plan.md requires that buying it
/// directly costs the same as buying it bundled.
/// </para>
/// <para>
/// These values are persisted. <b>Never reuse or renumber one.</b>
/// </para>
/// </remarks>
public enum PaymentPurpose
{
    IdentityVerification = 1,
    BusinessVerification = 2,
    PropertyVerification = 3,
}

/// <summary>
/// Where a payment attempt got to.
/// </summary>
/// <remarks>
/// A payment is only ever fulfilled from <see cref="Pending"/>, which is what makes
/// a replayed webhook harmless — see <see cref="Entities.Payment.TrySettle"/>.
/// </remarks>
public enum PaymentStatus
{
    /// <summary>Initialised with the gateway. Nothing has been confirmed.</summary>
    Pending = 1,

    Successful = 2,
    Failed = 3,

    /// <summary>The payer left the gateway without completing.</summary>
    Abandoned = 4,

    /// <summary>
    /// The gateway confirmed a payment that does not match what we asked for.
    /// </summary>
    /// <remarks>
    /// Held apart from a failure because nothing is wrong with the <i>payment</i> —
    /// money may well have moved. What is wrong is that the amount does not match
    /// the amount recorded when the attempt was created, and the only safe response
    /// is to hand nothing over and have a person look at it. Deliberately terminal
    /// as far as settlement goes: a later webhook must not quietly settle it. It
    /// can, however, be refunded — that is usually the right resolution.
    /// </remarks>
    Flagged = 5,

    /// <summary>A refund has been asked of the provider and not yet confirmed.</summary>
    /// <remarks>
    /// Its own state rather than a flag, because the money has not moved back yet
    /// and saying it has would be a lie to whoever is reading the receipt. Paystack
    /// answers most refunds as pending and confirms by webhook.
    /// </remarks>
    RefundPending = 6,

    /// <summary>The provider confirmed the money went back.</summary>
    /// <remarks>
    /// <b>No longer settled.</b> <c>IsSettled</c> is true only for
    /// <see cref="Successful"/>, so a refunded payment stops satisfying the
    /// verification gate — somebody whose money has been returned has not paid for
    /// a review. That falls out of the definition rather than needing a special
    /// case, which is why the definition is worth keeping narrow.
    /// </remarks>
    Refunded = 7,
}

/// <summary>Outcome of attempting to settle a payment. See <see cref="Entities.Payment.TrySettle"/>.</summary>
public enum PaymentSettlementOutcome
{
    /// <summary>Settled by this call. The caller should now hand over what was bought.</summary>
    Settled = 1,

    /// <summary>
    /// Already settled by an earlier call. Not an error — gateways retry webhooks,
    /// and the caller should report success and do nothing.
    /// </summary>
    AlreadySettled = 2,

    /// <summary>The amount paid is not the amount asked for. The payment is now Flagged.</summary>
    AmountMismatch = 3,

    /// <summary>Terminal already — failed, abandoned or flagged. Cannot be settled.</summary>
    NotPending = 4,
}

/// <summary>Outcome of attempting to start or finish a refund.</summary>
public enum RefundOutcome
{
    /// <summary>Asked of the provider; awaiting confirmation.</summary>
    Requested = 1,

    /// <summary>The provider confirmed the money went back.</summary>
    Completed = 2,

    /// <summary>
    /// Already refunded, or a refund is already in flight. Not an error — refund
    /// webhooks are retried like any other, and a second request must not send a
    /// second refund.
    /// </summary>
    AlreadyInProgress = 3,

    /// <summary>Nothing to refund: the payment never succeeded.</summary>
    NotRefundable = 4,

    /// <summary>The provider refused, or could not be reached.</summary>
    Failed = 5,
}
