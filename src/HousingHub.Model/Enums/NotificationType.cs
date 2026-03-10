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
    InspectionCancelled = 4
}
