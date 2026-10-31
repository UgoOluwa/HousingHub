using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Payment;

/// <summary>A payment attempt, as a client may see it.</summary>
/// <remarks>
/// Carries no gateway credential and no raw provider payload. Amounts are in kobo,
/// as they are stored — the client formats for display, so there is exactly one
/// conversion and it is the last step.
/// </remarks>
public record PaymentDto(
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
    string? Channel,
    DateTime? PaidAt,
    DateTime DateCreated,
    /// <summary>Where to send the payer. Null once the attempt is no longer payable.</summary>
    string? AuthorisationUrl = null)
{
    /// <summary>
    /// The one place a payment is turned into its DTO.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A positional record is mapped by argument order, and two hand-written copies
    /// of a fourteen-argument call will eventually disagree. One did: a second copy
    /// passed the authorisation URL into <see cref="Channel"/> and left
    /// <see cref="AuthorisationUrl"/> null, so a payment that had been registered
    /// perfectly well came back with nowhere to send the payer — and the client,
    /// finding no URL, showed the success message as an error.
    /// </para>
    /// <para>
    /// The gateway link is offered only while the attempt is still payable. A stale
    /// one on a settled or failed payment is an invitation to pay twice.
    /// </para>
    /// </remarks>
    public static PaymentDto From(Model.Entities.Payment payment) => new(
        payment.Id,
        payment.Reference,
        payment.Purpose,
        payment.SubjectId,
        payment.AmountKobo,
        payment.PurposeFeeKobo,
        payment.IdentityFeeKobo,
        payment.IncludesIdentityVerification,
        payment.Currency,
        payment.Status,
        payment.Channel,
        payment.PaidAt,
        payment.DateCreated,
        payment.Status == Model.Enums.PaymentStatus.Pending ? payment.AuthorisationUrl : null);
}

/// <summary>
/// What a verification case will cost, before anyone is asked to pay.
/// </summary>
/// <remarks>
/// Exists so the price is shown before payment rather than after. The
/// non-refundability of a completed check has to be stated in plain words up front
/// — otherwise the platform earns chargebacks, which on a young merchant account
/// cost out of all proportion to the amount disputed.
/// </remarks>
public record PaymentQuoteDto(
    PaymentPurpose Purpose,
    long PurposeFeeKobo,
    long IdentityFeeKobo,
    long TotalKobo,
    string Currency,
    bool IncludesIdentityVerification,
    bool IsAlreadyPaid,
    /// <summary>
    /// False when charging is switched off entirely, in which case every other
    /// figure here is zero and the case can be submitted without paying.
    /// </summary>
    /// <remarks>
    /// Answered here rather than left for the client to infer from a failed quote.
    /// A client that had to read "we can't take payments" as "you don't need to pay"
    /// would be unable to tell that apart from a real outage — and would show a
    /// payment wall on an environment where verification is free.
    /// </remarks>
    bool IsPaymentRequired = true);
