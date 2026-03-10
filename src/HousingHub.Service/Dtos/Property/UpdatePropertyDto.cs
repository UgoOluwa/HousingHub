using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.PropertyAddress;

namespace HousingHub.Service.Dtos.Property;

public record UpdatePropertyDto(
    Guid Id,
    string? Title,
    string? Description,
    PropertyType? PropertyType,
    decimal? Price,
    PropertyAvailability? Availability,
    PropertyLeaseType? PropertyLeaseType,
    PropertyFeature? Features,
    string? ContactPersonName,
    string? ContactPersonEmail,
    string? ContactPersonPhoneNumber,
    UpdatePropertyAddressDto? PropertyAddress);
