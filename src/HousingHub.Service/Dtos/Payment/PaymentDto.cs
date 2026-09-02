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
    string? AuthorisationUrl = null);

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
    bool IsAlreadyPaid);
