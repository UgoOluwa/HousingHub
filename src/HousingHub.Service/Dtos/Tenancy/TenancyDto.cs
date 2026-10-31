using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Tenancy;

/// <summary>A tenancy as either party may see it.</summary>
/// <remarks>
/// Amounts are kobo, as everywhere else on the wire — the clients convert for
/// display and there is exactly one place that divides by a hundred.
/// </remarks>
public record TenancyDto(
    Guid Id,
    Guid PropertyId,
    string? PropertyTitle,
    Guid LandlordCustomerId,
    string? LandlordName,
    Guid TenantCustomerId,
    string? TenantName,
    TenancyStatus Status,
    long AgreedRentKobo,
    PropertyLeaseType LeaseType,
    Guid SelectedFromInspectionId,
    DateTime SelectedAt,
    string? WithdrawnReason,
    DateTime? ClosedAt,
    DateTime DateCreated);

/// <summary>
/// Somebody the owner could choose, drawn from completed inspections.
/// </summary>
/// <remarks>
/// Deliberately carries no contact details. The owner picks on the strength of who
/// viewed the property and whether their identity has been checked; talking to them
/// happens through in-app messaging, which keeps an address or a phone number from
/// being handed over before anyone has agreed to anything.
/// </remarks>
public record TenancyCandidateDto(
    Guid CustomerId,
    string? Name,
    Guid InspectionId,
    DateTime InspectedOn,
    bool IsIdentityVerified);

/// <summary>Picks a candidate. The property comes from the route.</summary>
public record SelectCandidateDto(Guid CustomerId);

/// <summary>
/// Pulls out of a selection.
/// </summary>
/// <remarks>
/// The reason is optional but shown to the candidate verbatim, so it is the thing
/// that distinguishes "the owner changed their mind" from silence.
/// </remarks>
public record WithdrawTenancyDto(string? Reason);

/// <summary>
/// What a tenant is asked to pay, itemised.
/// </summary>
/// <remarks>
/// Every line is shown before anything is charged. The commission is ours and is
/// named as ours — a tenant who discovers it only on a bank statement is a tenant
/// who disputes it, and they agreed to the rent and the owner's fees long before
/// this screen.
/// </remarks>
public record TenancyPaymentQuoteDto(
    Guid TenancyId,
    TenancyStatus TenancyStatus,
    long RentKobo,
    long OwnerFeesKobo,
    long CommissionKobo,
    long ProcessingFeeKobo,
    long TotalKobo,
    string Currency,

    /// <summary>False until every document has been accepted.</summary>
    bool IsPayable,

    /// <summary>True once this tenancy has been paid for.</summary>
    bool IsAlreadyPaid,

    /// <summary>
    /// False when the owner has not said where to pay them.
    /// </summary>
    /// <remarks>
    /// Surfaced rather than hidden behind a failure: the tenant has done nothing
    /// wrong and can do nothing about it, so the screen says who is holding things
    /// up instead of refusing with a message about an account they have never seen.
    /// </remarks>
    bool OwnerCanBePaid,

    IReadOnlyList<TenancyFeeDto> Fees);

/// <summary>
/// Where to send the tenant after they have paid.
/// </summary>
/// <remarks>
/// Dropped unless it points at one of our own front ends. Unchecked it is an open
/// redirect at the exact moment somebody is expecting a receipt.
/// </remarks>
public record StartTenancyPaymentDto(string? CallbackUrl);
