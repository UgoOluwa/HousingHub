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
    int InspectionCount);
