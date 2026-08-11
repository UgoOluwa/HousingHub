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
    VerificationExpired = 9
}
