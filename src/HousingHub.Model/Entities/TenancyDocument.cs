using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

/// <summary>
/// One document the owner has asked the tenant for.
/// </summary>
/// <remarks>
/// <para>
/// The owner composes a set of these in one request: the compulsory agreement plus
/// any number of custom-named extras. Each carries its own mode, so a single request
/// can mix "upload your employment letter", "sign this agreement" and "print this,
/// sign it and send it back".
/// </para>
/// <para>
/// Structurally this is <see cref="VerificationDocument"/> with the landlord as
/// reviewer instead of an admin. The shape is copied deliberately — the submit,
/// review, reject-with-reason loop is proven — but the entities stay apart because
/// who may review them, and what a decision means, are different.
/// </para>
/// </remarks>
[DynamoDBTable("TenancyDocuments")]
public class TenancyDocument : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("TenancyId-index")]
    public Guid TenancyId { get; set; }

    /// <summary>What the owner called it. Shown to the tenant verbatim.</summary>
    /// <remarks>
    /// Free text because the whole point is that an owner can ask for something we
    /// did not anticipate. The agreement is the only one with a fixed meaning, and
    /// that is carried by <see cref="IsAgreement"/> rather than by its name.
    /// </remarks>
    public string Name { get; set; } = null!;

    /// <summary>Optional note from the owner about what is wanted.</summary>
    public string? Instructions { get; set; }

    public TenancyDocumentMode Mode { get; set; }

    /// <summary>
    /// True for the tenancy agreement itself.
    /// </summary>
    /// <remarks>
    /// Exactly one per tenancy, enforced when the request is composed. Held as a flag
    /// rather than inferred from the name because the name is the owner's to choose
    /// and this decides what the set means — a tenancy whose agreement was rejected
    /// is not nearly agreed, whatever else has been accepted.
    /// </remarks>
    public bool IsAgreement { get; set; }

    public TenancyDocumentStatus Status { get; set; } = TenancyDocumentStatus.Requested;

    /// <summary>
    /// The file the owner supplied for the tenant to sign.
    /// </summary>
    /// <remarks>
    /// Set for <see cref="TenancyDocumentMode.SignInApp"/> and
    /// <see cref="TenancyDocumentMode.SignOffline"/>, where there has to be something
    /// to sign. Null for <see cref="TenancyDocumentMode.Upload"/>, where the tenant
    /// is the one producing the document.
    /// </remarks>
    public string? SourceFileKey { get; set; }

    /// <summary>
    /// SHA-256 of <see cref="SourceFileKey"/>'s bytes, taken when the owner uploaded it.
    /// </summary>
    /// <remarks>
    /// Computed at upload, when the bytes are already in hand, rather than at signing.
    /// Hashing at signing would mean fetching the file back out of storage on a path
    /// where a failure stops somebody signing their tenancy agreement — and the hash
    /// would then be of whatever is in storage at that moment, which is the thing it
    /// is supposed to prove.
    /// </remarks>
    public string? SourceFileHash { get; set; }

    /// <summary>What the tenant sent back — their upload, or the signed scan.</summary>
    public string? SubmittedFileKey { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Why the owner sent it back. Shown to the tenant verbatim.</summary>
    /// <remarks>
    /// Required on rejection. "Rejected" with no reason gives the tenant nothing to
    /// act on and turns the loop into guesswork.
    /// </remarks>
    public string? RejectionReason { get; set; }

    // ── Electronic signature ────────────────────────────────────
    // What makes a signature hold up is attribution — who signed — and integrity —
    // what they signed. The signer's government ID has already been checked against
    // their account, which is the attribution leg. These are the rest.

    public DateTime? SignedAt { get; set; }

    /// <summary>Where the signature came from, as the audit trail.</summary>
    public string? SignerIpAddress { get; set; }

    public string? SignerUserAgent { get; set; }

    /// <summary>
    /// SHA-256 of the exact bytes signed.
    /// </summary>
    /// <remarks>
    /// The integrity leg. Without it "they signed the agreement" is a claim about a
    /// document that may since have been replaced; with it, the document either
    /// hashes to this or it is not the one that was signed.
    /// </remarks>
    public string? SignedDocumentHash { get; set; }

    /// <summary>True once the owner has accepted it and nothing more is needed.</summary>
    [DynamoDBIgnore]
    public bool IsSettled => Status == TenancyDocumentStatus.Accepted;

    /// <summary>True while the tenant still has something to do.</summary>
    [DynamoDBIgnore]
    public bool IsWithTenant =>
        Status is TenancyDocumentStatus.Requested or TenancyDocumentStatus.Rejected;

    public TenancyDocument() { }

    public TenancyDocument(
        Guid tenancyId,
        string name,
        TenancyDocumentMode mode,
        bool isAgreement,
        string? instructions,
        string? sourceFileKey,
        string? sourceFileHash)
    {
        Id = Guid.NewGuid();
        TenancyId = tenancyId;
        Name = name;
        Mode = mode;
        IsAgreement = isAgreement;
        Instructions = instructions;
        SourceFileKey = sourceFileKey;
        SourceFileHash = sourceFileHash;
        Status = TenancyDocumentStatus.Requested;
        IsActive = true;
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
    }

    /// <summary>
    /// The tenant returns a file — their own document, or a signed scan.
    /// </summary>
    /// <remarks>
    /// Allowed from Requested and from Rejected, because correcting a rejection is
    /// the same act as doing it the first time. Refused once accepted: a document
    /// the owner has agreed to must not change underneath them.
    /// </remarks>
    public bool TrySubmitFile(string fileKey)
    {
        if (!IsWithTenant) return false;
        if (Mode == TenancyDocumentMode.SignInApp) return false;

        SubmittedFileKey = fileKey;
        SubmittedAt = DateTime.UtcNow;
        Status = TenancyDocumentStatus.Submitted;

        // Cleared rather than kept: the reason described the previous attempt, and
        // leaving it attached would show the tenant a complaint about something they
        // have already fixed.
        RejectionReason = null;
        DateModified = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// The tenant signs in the app.
    /// </summary>
    /// <remarks>
    /// The hash is of the source document as served, so what was agreed is pinned to
    /// bytes rather than to a file name. No new file is produced here — the signed
    /// artefact is the source plus this record.
    /// </remarks>
    public bool TrySign(string documentHash, string? ipAddress, string? userAgent)
    {
        if (!IsWithTenant) return false;
        if (Mode != TenancyDocumentMode.SignInApp) return false;
        if (string.IsNullOrWhiteSpace(SourceFileKey)) return false;
        if (string.IsNullOrWhiteSpace(documentHash)) return false;

        SignedDocumentHash = documentHash;
        SignerIpAddress = ipAddress;
        SignerUserAgent = userAgent;
        SignedAt = DateTime.UtcNow;
        SubmittedAt = DateTime.UtcNow;
        Status = TenancyDocumentStatus.Submitted;
        RejectionReason = null;
        DateModified = DateTime.UtcNow;
        return true;
    }

    /// <summary>The owner accepts it.</summary>
    public bool TryAccept()
    {
        if (Status != TenancyDocumentStatus.Submitted) return false;

        Status = TenancyDocumentStatus.Accepted;
        ReviewedAt = DateTime.UtcNow;
        RejectionReason = null;
        DateModified = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// The owner sends it back.
    /// </summary>
    /// <remarks>
    /// A reason is required rather than optional. It is the only thing that tells the
    /// tenant what to change, and a rejection without one turns the loop into
    /// guesswork for the person least able to guess.
    /// </remarks>
    public bool TryReject(string reason)
    {
        if (Status != TenancyDocumentStatus.Submitted) return false;
        if (string.IsNullOrWhiteSpace(reason)) return false;

        Status = TenancyDocumentStatus.Rejected;
        RejectionReason = reason.Trim();
        ReviewedAt = DateTime.UtcNow;

        // The signature goes with the rejection. It attested to a submission the
        // owner has refused, and leaving it would leave a signed-but-rejected
        // document that reads as executed.
        SignedAt = null;
        SignedDocumentHash = null;
        SignerIpAddress = null;
        SignerUserAgent = null;

        DateModified = DateTime.UtcNow;
        return true;
    }
}
