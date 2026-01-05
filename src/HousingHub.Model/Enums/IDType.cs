using System.ComponentModel;

namespace HousingHub.Model.Enums;

public enum IDType
{
    [Description("National Identification Number")]
    NIN = 1,
    [Description("Driver's License")]
    DL = 2,
    [Description("Passport")]
    Passport = 3,
    [Description("Voter's Card")]
    VoterCard = 4,
    [Description("Other")]
    Other = 5
}
