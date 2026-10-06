using System.ComponentModel;

namespace HousingHub.Model.Enums;

public enum NotificationType
{
    [Description("Inspection Scheduled")]
    InspectionScheduled = 0,

    [Description("Inspection Confirmed")]
    InspectionConfirmed = 1,

    [Description("Inspection Declined")]
    InspectionDeclined = 2,

    [Description("Inspection Rescheduled")]
    InspectionRescheduled = 3,

    [Description("Inspection Cancelled")]
    InspectionCancelled = 4,

    [Description("New Message")]
    NewMessage = 5,

    [Description("Property Match")]
    PropertyMatch = 6,

    [Description("Verification Approved")]
    VerificationApproved = 7,

    [Description("Verification Rejected")]
    VerificationRejected = 8,

    /// <summary>
    /// A verification lapsed because one of its documents expired. Distinct from a
    /// rejection: nothing was wrong with the submission, it simply aged out, and the
    /// action the user needs to take is different.
    /// </summary>
    [Description("Verification Expired")]
    VerificationExpired = 9,

    /// <summary>
    /// A verification will lapse soon. Sent while the holder can still act on it —
    /// renewing a LASRERA registration takes weeks, so telling somebody on the day
    /// it drops is telling them too late.
    /// </summary>
    [Description("Verification Expiring Soon")]
    VerificationExpiringSoon = 10,

    /// <summary>
    /// An owner has chosen this person for a property.
    /// </summary>
    /// <remarks>
    /// The one notification in this list that changes somebody's housing situation,
    /// so it goes out in-app and by email rather than relying on them opening the app.
    /// </remarks>
    [Description("Selected For Property")]
    TenancyCandidateSelected = 11,

    /// <summary>The owner pulled out before the let completed.</summary>
    [Description("Selection Withdrawn")]
    TenancyWithdrawn = 12,

    /// <summary>The candidate is no longer interested. Sent to the owner.</summary>
    [Description("Candidate Declined")]
    TenancyDeclinedByCandidate = 13,

    /// <summary>The owner has sent the documents they need. Emailed as well.</summary>
    [Description("Documents Requested")]
    TenancyDocumentsRequested = 14,

    /// <summary>The tenant has returned or signed a document. Sent to the owner.</summary>
    [Description("Document Submitted")]
    TenancyDocumentSubmitted = 15,

    /// <summary>The owner accepted a document.</summary>
    [Description("Document Accepted")]
    TenancyDocumentAccepted = 16,

    /// <summary>
    /// The owner sent a document back.
    /// </summary>
    /// <remarks>
    /// Held apart from an acceptance because it is the one the tenant has to act on,
    /// and the reason travels with it.
    /// </remarks>
    [Description("Document Returned")]
    TenancyDocumentRejected = 17,

    /// <summary>Every document has been accepted. The next step is payment.</summary>
    [Description("Documents Complete")]
    TenancyDocumentsComplete = 18
}
