using System.ComponentModel;

namespace HousingHub.Model.Enums;

public enum PropertyReportStatus
{
    [Description("Pending")]
    Pending = 0,

    [Description("Reviewed")]
    Reviewed = 1,

    [Description("Dismissed")]
    Dismissed = 2
}
