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
    /// is to hand nothing over and have a person look at it. Deliberately terminal:
    /// a later webhook must not quietly settle it.
    /// </remarks>
    Flagged = 5,
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
