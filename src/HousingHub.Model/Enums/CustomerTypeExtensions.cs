namespace HousingHub.Model.Enums;

public static class CustomerTypeExtensions
{
    /// <summary>
    /// Account types allowed to create, update and delete property listings and
    /// their files, and to see the owner dashboard/inspection views.
    ///
    /// This is the single source of truth for that rule — it was previously
    /// duplicated across the property and property-file services, so adding a
    /// new type meant finding every copy.
    /// </summary>
    public static bool CanManageProperties(this CustomerType customerType) =>
        customerType.HasFlag(CustomerType.HouseOwner)
        || customerType.HasFlag(CustomerType.Agent)
        || customerType.HasFlag(CustomerType.Developer);

    /// <summary>
    /// Types a user may choose for themselves during onboarding.
    /// Deliberately excludes Admin and Unset.
    /// </summary>
    public static bool IsSelectableAtOnboarding(this CustomerType customerType) =>
        customerType is CustomerType.Customer
            or CustomerType.HouseOwner
            or CustomerType.Agent
            or CustomerType.Developer;
}
