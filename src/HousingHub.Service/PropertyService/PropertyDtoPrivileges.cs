using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Property;

namespace HousingHub.Service.PropertyService;

/// <summary>
/// Puts back the fields the public projection of a property drops.
/// </summary>
/// <remarks>
/// <para>
/// <c>Property -&gt; PropertyDto</c> redacts the lister's contact details and the
/// internal moderation fields on every map — see PropertyMapper for why the default
/// is inverted. This is how the callers who are entitled to them get them back.
/// </para>
/// <para>
/// Three callers qualify, and only three: the listing's own owner, an admin, and the
/// owner reading the response to their own write. Nothing here re-checks
/// entitlement, so every call site states its reason.
/// </para>
/// </remarks>
internal static class PropertyDtoPrivileges
{
    internal static PropertyDto WithPrivilegedFields(this PropertyDto dto, Property property) => dto with
    {
        ContactPersonEmail = property.ContactPersonEmail,
        ContactPersonPhoneNumber = property.ContactPersonPhoneNumber,
        UnpublishReason = property.UnpublishReason,
        IsFlaggedDuplicate = property.IsFlaggedDuplicate,
        PossibleDuplicateOfPropertyId = property.PossibleDuplicateOfPropertyId,
    };
}
