using System.ComponentModel;

namespace HousingHub.Model.Enums;

public enum InspectionStatus
{
    [Description("Pending")]
    Pending = 0,

    [Description("Confirmed")]
    Confirmed = 1,

    [Description("Declined")]
    Declined = 2,

    [Description("Rescheduled")]
    Rescheduled = 3,

    [Description("Completed")]
    Completed = 4,

    [Description("Cancelled")]
    Cancelled = 5
}
