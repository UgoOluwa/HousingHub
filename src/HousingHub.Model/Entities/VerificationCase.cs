using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

/// <summary>
/// One reviewable unit of verification: a subject, the documents supporting it,
/// and a single decision.
/// </summary>
/// <remarks>
/// <para>
/// The case exists so an admin approves a coherent claim — "this agency is
/// registered" — rather than seven loose files with no relationship to each other.
/// It is also what makes the pipeline generic: business, title and (later)
/// identity and financial verification are all a case with different document
/// types, not four separate features.
/// </para>
/// <para>
/// State transitions are enforced by the methods on this class rather than by
/// callers setting <see cref="Status"/> directly. That is deliberate: the
/// difference between Draft and Submitted is the difference between "the user owns
/// this" and "review owns this", and letting any caller move that boundary is how
/// a submitted case ends up being edited underneath the reviewer.
/// </para>
/// </remarks>
[DynamoDBTable("VerificationCases")]
public class VerificationCase : BaseEntity
{
    /// <summary>
    /// Customer.Id for a business or identity case, Property.Id for a title case.
    /// </summary>
    /// <remarks>
    /// Indexed so "show me everything about this subject" is a query rather than a
    /// scan. Not unique — a property can be re-verified after a title transfer, and
    /// the old case is kept as the evidence trail for what was believed at the time.
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("SubjectId-index")]
    public Guid SubjectId { get; set; }

    public VerificationSubjectType SubjectType { get; set; }

    /// <summary>
    /// The customer who submitted this, which is not always the subject.
    /// </summary>
    /// <remarks>
    /// For a title case the subject is a property but the submitter is a person, and
    /// authorization is about the submitter. Keeping it explicit avoids inferring
    /// "who may touch this" from SubjectType at every call site.
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("SubmittedByCustomerId-index")]
    public Guid SubmittedByCustomerId { get; set; }

    public VerificationTier RequestedTier { get; set; }

    /// <summary>
    /// Current state. Written through the transition methods, not directly.
    /// </summary>
    /// <remarks>
    /// Mirrored as a string in <see cref="ReviewQueueStatus"/> for indexing —
    /// DynamoDB cannot key on an enum stored as a number in a way the review queue
    /// can query efficiently.
    /// </remarks>
    public VerificationCaseStatus Status { get; set; } = VerificationCaseStatus.Draft;

    /// <summary>
    /// Index key marking a case that is waiting on an admin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sparse by design: only Submitted and UnderReview cases carry a value, so the
    /// review queue is a Query returning exactly the work outstanding, rather than a
    /// scan of every case ever decided. Approved and rejected cases accumulate
    /// forever and would otherwise make the queue slower every month.
    /// </para>
    /// <para>
    /// Derived, with a setter that discards its argument — same pattern and same
    /// reasoning as <see cref="Property.PublishedStatus"/>. Storing it independently
    /// would let it disagree with <see cref="Status"/>, and which one won would
    /// depend on the order the DynamoDB mapper assigns properties, which is not
    /// specified.
    /// </para>
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("ReviewQueueStatus-index")]
    public string? ReviewQueueStatus
    {
        get => Status is VerificationCaseStatus.Submitted or VerificationCaseStatus.UnderReview
            ? AwaitingReviewMarker
            : null;
        set { /* derived from Status — see remarks */ }
    }

    /// <summary>Attribute value written for a case awaiting an admin decision.</summary>
    public const string AwaitingReviewMarker = "AWAITING_REVIEW";

    /// <summary>
    /// Index key marking an approved case that can one day lapse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sparse, and doubly so: present only when the case is <b>approved</b> and has
    /// an expiry at all. Approved cases accumulate forever and most of them never
    /// expire — a Certificate of Occupancy does not lapse — so without this the
    /// nightly sweep would read every case ever decided in order to find the handful
    /// of LASRERA permits that ran out.
    /// </para>
    /// <para>
    /// Derived with a discarding setter, same as <see cref="ReviewQueueStatus"/>. The
    /// two must never disagree, and which one won would otherwise depend on the order
    /// the DynamoDB mapper assigns properties.
    /// </para>
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("ExpiryWatch-index")]
    public string? ExpiryWatch
    {
        get => Status == VerificationCaseStatus.Approved && ExpiresAt.HasValue
            ? ExpiryWatchMarker
            : null;
        set { /* derived from Status and ExpiresAt — see remarks */ }
    }

    /// <summary>Attribute value written for an approved case that carries an expiry.</summary>
    public const string ExpiryWatchMarker = "WATCH_EXPIRY";

    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedByAdminId { get; set; }

    /// <summary>Shown to the submitter, so it must be written for them to read.</summary>
    public string? DecisionNote { get; set; }

    /// <summary>
    /// Earliest expiry among the approved documents, or null if none expire.
    /// </summary>
    /// <remarks>
    /// Denormalised onto the case so "which verifications lapse this month" does not
    /// require reading every document. A case is only as current as its
    /// shortest-lived evidence — LASRERA permits lapse annually, so a business
    /// verification is a statement with a shelf life, not a permanent fact.
    /// </remarks>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Smallest reminder threshold already sent, in days before expiry. Null means
    /// none have gone out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One field rather than a timestamp per threshold, because the thresholds are
    /// ordered and only the most recent one matters. Storing 30 means the thirty-day
    /// warning went out; storing 7 means both did. Adding a fourteen-day threshold
    /// later needs no schema change.
    /// </para>
    /// <para>
    /// This is what makes the daily sweep idempotent. Without it, a worker running
    /// every day for a month would send the same "expires in 30 days" email thirty
    /// times, which is worse than not warning at all — people filter a sender who
    /// does that.
    /// </para>
    /// </remarks>
    public int? LastExpiryReminderThreshold { get; set; }

    /// <summary>
    /// True when a reminder for this threshold has not yet been sent.
    /// </summary>
    /// <param name="daysOut">The threshold being considered, e.g. 30 or 7.</param>
    public bool NeedsExpiryReminder(int daysOut) =>
        LastExpiryReminderThreshold is null || LastExpiryReminderThreshold > daysOut;

    /// <summary>Records that the reminder for this threshold has gone out.</summary>
    public void MarkExpiryReminderSent(int daysOut)
    {
        LastExpiryReminderThreshold = daysOut;
        DateModified = DateTime.UtcNow;
    }

    [DynamoDBIgnore]
    public ICollection<VerificationDocument> Documents { get; set; } = new List<VerificationDocument>();

    public VerificationCase() { }

    public VerificationCase(
        Guid subjectId,
        VerificationSubjectType subjectType,
        Guid submittedByCustomerId,
        VerificationTier requestedTier)
    {
        Id = Guid.NewGuid();
        SubjectId = subjectId;
        SubjectType = subjectType;
        SubmittedByCustomerId = submittedByCustomerId;
        RequestedTier = requestedTier;
        Status = VerificationCaseStatus.Draft;
        IsActive = true;
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
    }

    /// <summary>
    /// True while the submitter may still add or remove documents.
    /// </summary>
    /// <remarks>
    /// Only in Draft. Once submitted, the set of documents is the thing being
    /// reviewed — allowing changes would mean an admin could approve a case whose
    /// contents changed after they looked at it.
    /// </remarks>
    [DynamoDBIgnore]
    public bool CanAcceptDocuments => Status == VerificationCaseStatus.Draft;

    /// <summary>True when the submitter may still withdraw or edit this.</summary>
    [DynamoDBIgnore]
    public bool IsOwnedBySubmitter => Status == VerificationCaseStatus.Draft;

    /// <summary>True when an admin decision is still outstanding.</summary>
    [DynamoDBIgnore]
    public bool IsAwaitingReview =>
        Status is VerificationCaseStatus.Submitted or VerificationCaseStatus.UnderReview;

    /// <summary>
    /// Hands the case to review. Returns false if it was not in Draft.
    /// </summary>
    public bool TrySubmit()
    {
        if (Status != VerificationCaseStatus.Draft) return false;

        Status = VerificationCaseStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Marks the case as actively being looked at, so two admins do not duplicate
    /// the work. Returns false if it was not merely Submitted.
    /// </summary>
    public bool TryBeginReview(Guid adminId)
    {
        if (Status != VerificationCaseStatus.Submitted) return false;

        Status = VerificationCaseStatus.UnderReview;
        DecidedByAdminId = adminId;
        DateModified = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Records a final decision. Returns false unless the case is awaiting one.
    /// </summary>
    /// <param name="status">Approved, Rejected, or EscalatedNameMismatch.</param>
    /// <param name="adminId">Who decided, for the audit trail.</param>
    /// <param name="note">Shown to the submitter. Required for anything but approval.</param>
    /// <param name="expiresAt">Earliest expiry among approved documents.</param>
    public bool TryDecide(
        VerificationCaseStatus status,
        Guid adminId,
        string? note,
        DateTime? expiresAt = null)
    {
        if (!IsAwaitingReview) return false;

        if (status is not (VerificationCaseStatus.Approved
            or VerificationCaseStatus.Rejected
            or VerificationCaseStatus.EscalatedNameMismatch))
        {
            return false;
        }

        // A rejection the submitter cannot act on is a support ticket. Escalation is
        // held to the same standard because the reviewer's reasoning is the only
        // record of why a name mismatch was treated as fraud rather than a typo.
        if (status != VerificationCaseStatus.Approved && string.IsNullOrWhiteSpace(note))
            return false;

        Status = status;
        DecidedByAdminId = adminId;
        DecidedAt = DateTime.UtcNow;
        DecisionNote = note;
        ExpiresAt = status == VerificationCaseStatus.Approved ? expiresAt : null;
        DateModified = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Moves an approved case to Expired once its earliest document has lapsed.
    /// Returns false if it was not approved, or has not expired yet.
    /// </summary>
    public bool TryExpire(DateTime asOf)
    {
        if (Status != VerificationCaseStatus.Approved) return false;
        if (ExpiresAt is null || ExpiresAt > asOf) return false;

        Status = VerificationCaseStatus.Expired;
        DateModified = DateTime.UtcNow;
        return true;
    }
}
