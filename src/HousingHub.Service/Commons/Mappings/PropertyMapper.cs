using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyFile;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class PropertyMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // ── The public projection ───────────────────────────────
        //
        // Property -> PropertyDto drops owner contact details and internal
        // moderation fields, and every path that maps a Property gets that
        // behaviour whether it remembered to ask for it or not.
        //
        // Inverted deliberately. A pen test found /Property/all, /Property/{id},
        // /Property/trending and /Property/nearby — all anonymous, because this is a
        // public listings site — returning the lister's email address and phone
        // number, plus UnpublishReason, IsFlaggedDuplicate and
        // PossibleDuplicateOfPropertyId. Redacting at each call site would have
        // fixed those four and left the next anonymous endpoint to leak again; this
        // codebase has repeatedly shipped a rule stated in one place and not
        // honoured in another. Owner and admin paths re-attach what they are
        // entitled to through WithOwnerOnlyFields, so forgetting now fails closed.
        //
        // OwnerId stays: it is not contact information, and the consumer app decides
        // whether to show owner controls by comparing it to the signed-in user.
        // OwnerName stays too — a listing says who is offering it, and the
        // verification badge is meaningless without it.
        // Mapped to empty rather than Ignore()d: PropertyDto is a positional record,
        // so Mapster maps it through its constructor, and removing a member from the
        // map leaves the wrong number of arguments — it fails at map time with
        // "Incorrect number of arguments for constructor", not at startup.
        config.NewConfig<Property, PropertyDto>()
            .Map(dest => dest.Files, src => src.Files)
            .Map(dest => dest.ContactPersonEmail, src => (string?)null)
            .Map(dest => dest.ContactPersonPhoneNumber, src => (string?)null)
            .Map(dest => dest.UnpublishReason, src => (string?)null)
            .Map(dest => dest.IsFlaggedDuplicate, src => false)
            .Map(dest => dest.PossibleDuplicateOfPropertyId, src => (Guid?)null);

        // PropertyDto -> Property is a separate config rather than TwoWays(), because
        // the ignores above must not travel back: a write path mapping a DTO onto an
        // entity has to be able to set the contact fields.
        config.NewConfig<PropertyDto, Property>();
        config.NewConfig<PropertyFile, PropertyFileDto>().TwoWays();
        config.NewConfig<CreatePropertyDto, Property>();
        config.NewConfig<UpdatePropertyDto, Property>();
    }
}
