using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Admin;

public record AdminPropertyFilterDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsPublished = null,
    PropertyAvailability? Availability = null,
    bool? FlaggedDuplicateOnly = null,

    /// <summary>
    /// Show only live listings whose owner has not passed identity verification —
    /// the backlog left by listings published before that became a requirement.
    /// </summary>
    bool? UnverifiedOwnerOnly = null);
