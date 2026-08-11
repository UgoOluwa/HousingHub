using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Admin;

public record AdminPropertyListDto(
    Guid Id,
    string PropertyId,
    string Title,
    string OwnerName,
    string Address,
    DateTime DatePosted,
    bool IsPublished,
    DateTime? PublishedAt,
    PropertyAvailability Availability,
    decimal Price,
    int InspectionCount,
    string? ThumbnailUrl = null,
    bool IsFlaggedDuplicate = false,
    Guid? PossibleDuplicateOfPropertyId = null,
    string? PossibleDuplicateOfTitle = null,

    /// <summary>Whether this listing's owner has passed identity verification.</summary>
    bool IsOwnerKycVerified = false,

    /// <summary>
    /// Live listing whose owner is not identity-verified.
    /// </summary>
    /// <remarks>
    /// Publishing now requires a verified owner, but listings published before that
    /// rule existed were left live rather than pulled out from under people. This
    /// marks them so they can be worked through by hand — combine with the
    /// <c>UnverifiedOwnerOnly</c> filter to get the backlog as a worklist.
    ///
    /// It can also be set by an admin publishing on an unverified owner's behalf,
    /// which is allowed but should be visible.
    /// </remarks>
    bool IsPublishedWithUnverifiedOwner = false);
