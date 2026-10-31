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
    Cancelled = 5,

    /// <summary>
    /// The slot passed, we asked the renter whether the viewing happened, and they
    /// never said.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not the same as cancelled. Cancelled means somebody decided; this means
    /// nobody did, and we are not willing to keep claiming a viewing took place on
    /// the strength of a booking. It matters because a completed inspection is what
    /// qualifies a renter to be chosen as a tenant.
    /// </para>
    /// <para>
    /// Terminal. A renter who still wants the property books again — which is a real
    /// signal, where reviving a viewing nobody confirmed at the time is not.
    /// </para>
    /// </remarks>
    [Description("Expired")]
    Expired = 6
}
