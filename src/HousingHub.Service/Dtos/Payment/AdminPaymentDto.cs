using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Payment;

/// <summary>
/// A payment as an admin needs to see it.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="PaymentDto"/> rather than a superset of it, because the
/// two audiences need different things. This one carries who paid and, for a flagged
/// payment, why — neither of which belongs in a response to the payer. It carries no
/// gateway link, because an admin has no business resuming somebody else's checkout.
/// </para>
/// <para>
/// The payer's name and email are resolved here so the queue is readable. A row
/// showing only a customer id is a row an admin has to go and look up before they
/// can do anything with it.
/// </para>
/// </remarks>
public record AdminPaymentDto(
    Guid Id,
    string Reference,
    PaymentPurpose Purpose,
    Guid? SubjectId,
    long AmountKobo,
    long PurposeFeeKobo,
    long IdentityFeeKobo,
    bool IncludesIdentityVerification,
    string Currency,
    PaymentStatus Status,
    string? Provider,
    string? ProviderReference,
    string? Channel,
    DateTime? PaidAt,
    DateTime DateCreated,
    Guid CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string? FailureReason,

    /// <summary>
    /// Why this needs a person. Set only on a flagged payment.
    /// </summary>
    /// <remarks>
    /// A flagged payment means money may well have moved while nothing was handed
    /// over — the one state in this system that cannot be resolved by code and must
    /// be resolved by somebody reading it.
    /// </remarks>
    string? FlagNote,

    // ── Refunds ─────────────────────────────────────────────────
    // Who sent money back, why, when, and how much. Answerable from the row rather
    // than from a log that rotates, because this is the only action in the system
    // that moves money out.

    string? RefundReason = null,
    Guid? RefundedByAdminId = null,
    DateTime? RefundRequestedAt = null,
    DateTime? RefundedAt = null,

    /// <summary>
    /// What actually went back. Not assumed to equal <see cref="AmountKobo"/> — a
    /// flagged payment is refunded for what arrived, not for what was asked.
    /// </summary>
    long? RefundAmountKobo = null,
    string? ProviderRefundReference = null);
