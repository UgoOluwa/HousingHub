using System.ComponentModel;

namespace HousingHub.Model.Enums;

[Flags]
public enum CustomerType
{
    /// <summary>
    /// Account created via an external provider (Google) before the user has told us
    /// how they intend to use Housing Hub. Carries no permissions.
    /// </summary>
    [Description("Not set")]
    Unset = 0,

    [Description("House Owner")]
    HouseOwner = 1,
    [Description("Agent")]
    Agent = 2,
    [Description("Customer")]
    Customer = 4,
    [Description("Admin")]
    Admin = 8,

    /// <summary>
    /// Property developer. Has the same property-management capabilities as a
    /// house owner or agent — see <see cref="CustomerTypeExtensions.CanManageProperties"/>.
    /// </summary>
    [Description("Developer")]
    Developer = 16
}
