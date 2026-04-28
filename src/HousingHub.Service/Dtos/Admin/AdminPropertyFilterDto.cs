using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Admin;

public record AdminPropertyFilterDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsPublished = null,
    PropertyAvailability? Availability = null);
