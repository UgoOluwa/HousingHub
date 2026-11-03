using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Inspection;

public record InspectionDto(
    Guid Id,
    string InspectionId,
    DateTime DateCreated,
    DateTime DateModified,
    Guid CustomerId,
    Guid PropertyId,
    DateTime ScheduledDate,
    TimeSpan ScheduledTime,
    string? Note,
    InspectionStatus Status,
    string? DeclineNote,
    DateTime? RescheduledDate,
    TimeSpan? RescheduledTime,
    string? RescheduleNote,
    /// <summary>Who proposed the pending reschedule; the other party is the one who answers it.</summary>
    Guid? RescheduleRequestedById,
    string? PropertyImageUrl = null,
    Guid? PropertyOwnerId = null,
    string? CustomerName = null,

    /// <summary>
    /// Where the person coming to view is based — city and state, nothing finer.
    /// </summary>
    /// <remarks>
    /// Deliberately not the street address. The viewing happens at the owner's
    /// property, so how far somebody is travelling is useful and where they live is
    /// not; handing over a home address before either party has agreed to anything
    /// is a leak rather than a feature. Null when they have not set an address.
    /// </remarks>
    string? CustomerLocation = null,

    /// <summary>When the renter confirmed the viewing happened.</summary>
    DateTime? CompletedAt = null,

    /// <summary>
    /// What the renter said about the viewing, if anything.
    /// </summary>
    /// <remarks>
    /// Goes to both parties. It is the only written account of what happened, and an
    /// owner deciding whether to choose this person should read it.
    /// </remarks>
    string? CompletionComment = null,
    string? PropertyOwnerName = null,
    DateTime? HandedOffAt = null,
    Guid? AssignedStaffId = null,
    string? AssignedStaffName = null);
