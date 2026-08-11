using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Verification;

/// <summary>Opens a draft case.</summary>
/// <param name="SubjectType">Business or Property.</param>
/// <param name="SubjectId">
/// The property being verified. Ignored for a business case, where the subject is
/// always the authenticated caller — accepting it from the body there would let
/// somebody open a case against another person's account.
/// </param>
public record StartVerificationCaseDto(
    VerificationSubjectType SubjectType,
    Guid? SubjectId = null);

/// <summary>
/// Metadata declared alongside an uploaded document.
/// </summary>
/// <remarks>
/// Declared by the submitter, not extracted. Everything here is a claim to be
/// checked against the document itself during review — which is the point:
/// <see cref="NameOnDocument"/> is what the submitter says the document says, and
/// the reviewer's job is partly to confirm they were telling the truth.
/// </remarks>
public record AddVerificationDocumentDto(
    VerificationDocumentType DocumentType,
    string? DocumentNumber = null,
    string? NameOnDocument = null,
    string? IssuingAuthority = null,
    DateTime? IssuedAt = null,
    DateTime? ExpiresAt = null);

/// <summary>A per-document review decision.</summary>
/// <param name="Approved">False rejects the document.</param>
/// <param name="RejectionReason">Required when rejecting. Shown to the submitter.</param>
public record ReviewDocumentDto(bool Approved, string? RejectionReason = null);

/// <summary>The final decision on a case.</summary>
/// <param name="Outcome">Approved, Rejected, or EscalatedNameMismatch.</param>
/// <param name="Note">Required for anything other than approval.</param>
public record DecideCaseDto(VerificationCaseStatus Outcome, string? Note = null);

/// <summary>Summary of a case, for lists and queues.</summary>
public record VerificationCaseDto(
    Guid Id,
    Guid SubjectId,
    VerificationSubjectType SubjectType,
    Guid SubmittedByCustomerId,
    VerificationTier RequestedTier,
    VerificationCaseStatus Status,
    DateTime DateCreated,
    DateTime? SubmittedAt,
    DateTime? DecidedAt,
    string? DecisionNote,
    DateTime? ExpiresAt,
    int DocumentCount,
    // Populated on the review queue so a reviewer can triage without opening each case.
    string? SubjectLabel = null,
    string? SubmittedByName = null);

/// <summary>
/// One document as returned to a client.
/// </summary>
/// <remarks>
/// Carries no URL and no storage key. The key is an S3 path to a title deed or a
/// company record — handing it to the browser gives away where the object lives
/// and invites someone to try fetching it directly. Clients call the
/// document-url endpoint, which mints a link that expires.
/// </remarks>
public record VerificationDocumentDto(
    Guid Id,
    VerificationDocumentType DocumentType,
    string? OriginalFileName,
    long FileSizeInBytes,
    string? DocumentNumber,
    string? NameOnDocument,
    string? IssuingAuthority,
    DateTime? IssuedAt,
    DateTime? ExpiresAt,
    DocumentReviewStatus Status,
    string? RejectionReason,
    DateTime? ReviewedAt,
    bool? AutoCheckPassed,
    string? AutoCheckProvider);

/// <summary>A case with its documents.</summary>
public record VerificationCaseDetailDto(
    VerificationCaseDto Case,
    List<VerificationDocumentDto> Documents,
    // Document types still required for the requested tier. Drives the submitter's
    // checklist, and is why a refused submit can say what is missing rather than
    // failing generically.
    List<VerificationDocumentType> MissingRequiredDocuments);
