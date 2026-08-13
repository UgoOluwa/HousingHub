using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

/// <summary>
/// One uploaded file within a <see cref="VerificationCase"/>, with its own review
/// state.
/// </summary>
/// <remarks>
/// Per-document status rather than one flag on the case, because the common
/// outcome is not "approved" or "rejected" but "five of these are fine and the
/// sixth is illegible". Without it the reviewer's only option is to reject
/// everything and make the submitter re-upload documents that were already good —
/// which is how a verification flow acquires a reputation for being painful.
/// </remarks>
[DynamoDBTable("VerificationDocuments")]
public class VerificationDocument : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("VerificationCaseId-index")]
    public Guid VerificationCaseId { get; set; }

    public VerificationDocumentType DocumentType { get; set; }

    /// <summary>
    /// S3 object key — <b>not</b> a URL.
    /// </summary>
    /// <remarks>
    /// These are title deeds and company records: private by default, stored under
    /// the private prefix, and only ever reachable through a short-lived presigned
    /// URL minted for an authorised caller. Storing a URL here is what went wrong
    /// with KYC originally, and the migration is still outstanding — see
    /// docs/data-backfill-required.md.
    /// </remarks>
    public string StorageKey { get; set; } = null!;

    /// <summary>Original filename, for the reviewer's benefit only. Never used to build a path.</summary>
    public string? OriginalFileName { get; set; }

    public string? ContentType { get; set; }
    public long FileSizeInBytes { get; set; }

    // ── Declared metadata ───────────────────────────────────────

    /// <summary>RC number, C of O number, permit number.</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>
    /// The name printed on the document.
    /// </summary>
    /// <remarks>
    /// The most important field here. Nearly every fraud in this space is a real
    /// document belonging to somebody else: a genuine CAC certificate, a genuine C
    /// of O, submitted by a person with no connection to it. Comparing this against
    /// the verified account holder is the check that catches it, and it is the
    /// reason <see cref="VerificationCaseStatus.EscalatedNameMismatch"/> exists as
    /// its own outcome rather than a flavour of rejection.
    /// </remarks>
    public string? NameOnDocument { get; set; }

    /// <summary>"CAC", "Lagos State", "LASRERA".</summary>
    public string? IssuingAuthority { get; set; }

    public DateTime? IssuedAt { get; set; }

    /// <summary>
    /// When this document stops being evidence. Null means it does not expire.
    /// </summary>
    /// <remarks>
    /// LASRERA registrations lapse annually. A C of O does not expire. Both are
    /// normal, which is why this is nullable rather than defaulted to some horizon.
    /// </remarks>
    public DateTime? ExpiresAt { get; set; }

    // ── Review ──────────────────────────────────────────────────

    public DocumentReviewStatus Status { get; set; } = DocumentReviewStatus.Pending;

    public string? RejectionReason { get; set; }
    public Guid? ReviewedByAdminId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // ── Automated check ─────────────────────────────────────────

    /// <summary>
    /// Result of a provider lookup, where one exists. Null means nothing was run.
    /// </summary>
    /// <remarks>
    /// Advisory only, and deliberately separate from <see cref="Status"/>. Nigerian
    /// registries are inconsistent enough that a failed lookup is often a bad
    /// record rather than a bad applicant, and a passing lookup only confirms the
    /// number exists — not that the person submitting it has any right to it. The
    /// human decision stays authoritative; this informs it.
    /// </remarks>
    public bool? AutoCheckPassed { get; set; }

    /// <summary>"Dojah", "QoreID", "Mono".</summary>
    public string? AutoCheckProvider { get; set; }

    public DateTime? AutoCheckedAt { get; set; }

    /// <summary>
    /// Provider response, for the audit trail.
    /// </summary>
    /// <remarks>
    /// Kept because when a verification decision is later disputed, "what did the
    /// registry actually say on the day" is the question, and providers change
    /// their responses over time.
    ///
    /// Truncated on write — DynamoDB items are capped at 400KB and a verbose
    /// provider payload could otherwise fail the whole save.
    /// </remarks>
    public string? AutoCheckRawResponse { get; set; }

    public VerificationDocument() { }

    public VerificationDocument(
        Guid verificationCaseId,
        VerificationDocumentType documentType,
        string storageKey,
        string? originalFileName,
        string? contentType,
        long fileSizeInBytes)
    {
        Id = Guid.NewGuid();
        VerificationCaseId = verificationCaseId;
        DocumentType = documentType;
        StorageKey = storageKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        FileSizeInBytes = fileSizeInBytes;
        Status = DocumentReviewStatus.Pending;
        IsActive = true;
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
    }

    /// <summary>True when this document is currently valid evidence.</summary>
    [DynamoDBIgnore]
    public bool IsValidEvidence =>
        Status == DocumentReviewStatus.Approved
        && (ExpiresAt is null || ExpiresAt > DateTime.UtcNow);

    /// <summary>
    /// Records a per-document review decision.
    /// </summary>
    /// <returns>False if rejected without a reason, which the submitter could not act on.</returns>
    public bool TryReview(DocumentReviewStatus status, Guid adminId, string? rejectionReason)
    {
        if (status == DocumentReviewStatus.Rejected && string.IsNullOrWhiteSpace(rejectionReason))
            return false;

        Status = status;
        RejectionReason = status == DocumentReviewStatus.Rejected ? rejectionReason : null;
        ReviewedByAdminId = adminId;
        ReviewedAt = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        return true;
    }
}
