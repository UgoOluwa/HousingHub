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
