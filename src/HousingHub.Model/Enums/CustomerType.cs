using System.ComponentModel;

namespace HousingHub.Model.Enums;

[Flags]
public enum CustomerType
{
    [Description("House Owner")]
    HouseOwner = 1,
    [Description("Agent")]
    Agent = 2,
    [Description("Renter")]
    Renter = 4
}
