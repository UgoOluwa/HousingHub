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
    TenancyDeclinedByCandidate = 13
}
