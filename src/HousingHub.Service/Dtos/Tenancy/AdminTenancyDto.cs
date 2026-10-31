using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Tenancy;

/// <summary>
/// A tenancy as staff need to see it.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="TenancyDto"/> rather than a superset, because the two
/// audiences need different things. This one names both parties and their email
/// addresses so a row can be acted on without three more lookups, and it answers
/// the question staff actually have — <i>who is the hold-up</i> — with counts
/// rather than a list the caller has to total up.
/// </para>
/// <para>
/// No document contents and no file links: those live on the detail response, so
/// a listing cannot leak a tenant's paperwork into a screen that only meant to
/// show a queue.
/// </para>
/// </remarks>
public record AdminTenancyDto(
    Guid Id,
    Guid PropertyId,
    string? PropertyTitle,

    Guid LandlordCustomerId,
    string? LandlordName,
    string? LandlordEmail,

    Guid TenantCustomerId,
    string? TenantName,
    string? TenantEmail,

    TenancyStatus Status,
    PropertyLeaseType LeaseType,

    /// <summary>Snapshotted when the tenant was chosen. Not the listing's current price.</summary>
    long AgreedRentKobo,
    long FeesKobo,
    long TotalKobo,

    int DocumentCount,
    int AcceptedDocumentCount,

    /// <summary>Asked for or sent back — the tenant has to move.</summary>
    int AwaitingTenantCount,

    /// <summary>Submitted and unreviewed — the owner has to move.</summary>
    int AwaitingOwnerCount,

    DateTime SelectedAt,
    string? WithdrawnReason,
    DateTime? ClosedAt,
    DateTime DateCreated);

/// <summary>
/// One document, with the signature audit trail attached.
/// </summary>
/// <remarks>
/// <para>
/// The audit fields are the point of this DTO. If a tenant later denies signing,
/// what answers it is the time, the address, the device and the hash of the exact
/// bytes that were signed — and all four have to be readable by a person without a
/// database console.
/// </para>
/// <para>
/// Carries no storage keys. Staff reach a file through a short-lived link, so that
/// every access is a request that can be refused and counted.
/// </para>
/// </remarks>
public record AdminTenancyDocumentDto(
    Guid Id,
    Guid TenancyId,
    string Name,
    string? Instructions,
    TenancyDocumentMode Mode,
    bool IsAgreement,
    TenancyDocumentStatus Status,
    bool HasSourceFile,
    bool HasSubmittedFile,
    bool HasSignedPdf,
    DateTime? SubmittedAt,
    DateTime? ReviewedAt,
    string? RejectionReason,

    // ── Signature audit ─────────────────────────────────────────
    DateTime? SignedAt,
    string? SignerIpAddress,
    string? SignerUserAgent,

    /// <summary>SHA-256 of the file the owner supplied, taken when it was stored.</summary>
    string? SourceFileHash,

    /// <summary>What the signature attests to. Equals the source hash for an untampered document.</summary>
    string? SignedDocumentHash,

    DateTime DateCreated);

/// <summary>
/// Somebody who viewed the property, and whether they were the one chosen.
/// </summary>
/// <remarks>
/// Staff see everyone who was in the running, not just the person picked. A
/// complaint about a letting decision is a complaint about who else there was, and
/// answering it from the selected row alone is answering a different question.
/// </remarks>
public record AdminTenancyCandidateDto(
    Guid CustomerId,
    string? Name,
    string? Email,
    Guid InspectionId,
    DateTime InspectedOn,
    DateTime? ConfirmedByRenterAt,
    string? RenterComment,
    bool WasChosen);

/// <summary>A payment against this tenancy, as staff need it.</summary>
public record AdminTenancyPaymentDto(
    Guid Id,
    string Reference,
    PaymentStatus Status,
    long AmountKobo,
    /// <summary>What we kept. The rest went straight to the owner.</summary>
    long? PlatformChargeKobo,
    long PaidToOwnerKobo,
    DateTime? PaidAt,
    DateTime DateCreated,
    string? RefundReason,
    DateTime? RefundedAt,
    long? RefundAmountKobo);

/// <summary>One tenancy in full: the parties, the money, the paperwork and the fees.</summary>
public record AdminTenancyDetailDto(
    AdminTenancyDto Tenancy,
    IReadOnlyList<AdminTenancyDocumentDto> Documents,
    IReadOnlyList<TenancyFeeDto> Fees,

    /// <summary>Everyone who completed a viewing on this property, chosen or not.</summary>
    IReadOnlyList<AdminTenancyCandidateDto> Candidates,

    /// <summary>Every payment attempt against this tenancy, newest first.</summary>
    IReadOnlyList<AdminTenancyPaymentDto> Payments);
