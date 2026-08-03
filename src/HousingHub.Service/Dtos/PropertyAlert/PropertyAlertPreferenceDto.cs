using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.PropertyAlert;

public record PropertyAlertPreferenceDto(
    Guid Id,
    DateTime DateCreated,
    PropertyType? PropertyType,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? City,
    string? State,
    PropertyFeature? Features,
    bool IsActive);

public record CreatePropertyAlertPreferenceDto(
    PropertyType? PropertyType,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? City,
    string? State,
    PropertyFeature? Features);
