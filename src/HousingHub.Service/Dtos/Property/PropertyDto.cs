using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.PropertyAddress;
using HousingHub.Service.Dtos.PropertyFile;

namespace HousingHub.Service.Dtos.Property;

public record PropertyDto(
    Guid Id,
    string PropertyId,
    DateTime DateCreated,
    DateTime DateModified,
    string Title,
    string Description,
    PropertyType PropertyType,
    decimal Price,
    PropertyAvailability Availability,
    PropertyLeaseType PropertyLeaseType,
    PropertyFeature Features,
    string? ContactPersonName,
    string? ContactPersonEmail,
    string? ContactPersonPhoneNumber,
    Guid OwnerId,
    Guid AddressId,
    double? Latitude,
    double? Longitude,
    long ViewCount,
    bool IsPublished,
    DateTime? PublishedAt,
    bool IsVerified,
    DateTime? VerifiedAt,
    List<PropertyFileDto>? Files = null,
    // Count of open (Pending or Rescheduled) inspection requests for this property.
    int InspectionCount = 0,
    PropertyAddressDto? PropertyAddress = null,
    string? OwnerName = null,

    /// <summary>
    /// Whether the person behind this listing has passed identity verification.
    /// </summary>
    /// <remarks>
    /// This is the entire point of Phase 1 and it was previously computed nowhere and
    /// shown nowhere — identity documents were reviewed by hand and the renter, the
    /// person whose trust the review exists to earn, saw no difference.
    ///
    /// Note carefully what this does and does not assert. It means: a government ID
    /// was submitted and an admin checked it against the account holder. It does NOT
    /// mean the person owns this property, has the right to let it, or that the title
    /// is clean. Those are Phase 2 (title verification) and must not be implied by
    /// this flag or by any copy rendered from it.
    ///
    /// Distinct from <see cref="IsVerified"/>, which is a property-level moderation
    /// flag set by an admin against the listing rather than the person.
    /// </remarks>
    bool IsOwnerVerified = false,

    /// <summary>
    /// The highest verification the person behind this listing currently holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single tier rather than a flag per check, and the UI renders one badge from
    /// it. Two badges side by side invite a reader to average them into a general
    /// impression of safety, which is exactly the reasoning we are trying to prevent
    /// — the whole point of tiers is that each level makes a claim that is precisely
    /// true.
    /// </para>
    /// <para>
    /// <b>Current, not historical.</b> Computed through
    /// <c>Customer.IsBusinessVerified</c>, so a lapsed LASRERA permit drops the tier
    /// back to identity immediately rather than waiting for the nightly sweep. The
    /// sweep clears the stored tier; this makes sure nothing is displayed in the gap.
    /// </para>
    /// </remarks>
    Model.Enums.VerificationTier OwnerVerificationTier = Model.Enums.VerificationTier.Unverified,
    string? UnpublishReason = null,
    bool IsFlaggedDuplicate = false,
    Guid? PossibleDuplicateOfPropertyId = null);
