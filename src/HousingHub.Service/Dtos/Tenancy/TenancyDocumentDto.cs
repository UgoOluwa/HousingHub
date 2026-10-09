using HousingHub.Model.Enums;
using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.Dtos.Tenancy;

/// <summary>One requested document, as either party sees it.</summary>
/// <remarks>
/// Carries no storage keys. A key is an opaque reference the tenant has no use for,
/// and handing it out invites somebody to try addressing storage directly — files
/// are reached through a short-lived link from <c>GetDocumentUrlAsync</c> instead.
/// </remarks>
public record TenancyDocumentDto(
    Guid Id,
    Guid TenancyId,
    string Name,
    string? Instructions,
    TenancyDocumentMode Mode,
    bool IsAgreement,
    TenancyDocumentStatus Status,
    bool HasSourceFile,
    bool HasSubmittedFile,

    /// <summary>
    /// Whether there is a stamped PDF to download.
    /// </summary>
    /// <remarks>
    /// Not the same as being signed. The signature is the record; this is a
    /// convenience built from it, and it can be absent on a perfectly valid
    /// signature — so the client gates the download button on this and everything
    /// else on <see cref="SignedAt"/>.
    /// </remarks>
    bool HasSignedPdf,

    DateTime? SubmittedAt,
    DateTime? ReviewedAt,
    string? RejectionReason,
    DateTime? SignedAt,
    DateTime DateCreated);

/// <summary>A cost on top of the rent, named by the owner.</summary>
public record TenancyFeeDto(Guid Id, string Name, string Description, long AmountKobo);

/// <summary>
/// Everything the tenant is being asked for, and everything it will cost.
/// </summary>
/// <remarks>
/// Deliberately one response. The fees exist to be seen <i>before</i> anything is
/// signed, and splitting them into a second call is how a client ends up rendering
/// the documents without them.
/// </remarks>
public record TenancyDocumentPackDto(
    Guid TenancyId,
    TenancyStatus TenancyStatus,
    string? PropertyTitle,
    long AgreedRentKobo,
    IReadOnlyList<TenancyDocumentDto> Documents,
    IReadOnlyList<TenancyFeeDto> Fees,
    /// <summary>Rent plus every fee. What the tenant is agreeing to, in one number.</summary>
    long TotalKobo,
    /// <summary>True once every document has been accepted.</summary>
    bool IsComplete);

/// <summary>Adds one document to a request the owner is still composing.</summary>
public record AddTenancyDocumentDto(
    string Name,
    TenancyDocumentMode Mode,
    bool IsAgreement,
    string? Instructions,
    /// <summary>The document to be signed. Required for both signing modes.</summary>
    IFormFile? File = null);

/// <summary>Replaces the fee list wholesale.</summary>
public record SetTenancyFeesDto(IReadOnlyList<TenancyFeeInputDto> Fees);

public record TenancyFeeInputDto(string Name, string Description, long AmountKobo);

/// <summary>The owner's decision on one submitted document.</summary>
public record ReviewTenancyDocumentDto(bool Accept, string? RejectionReason);
