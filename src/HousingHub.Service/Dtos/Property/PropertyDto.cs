using HousingHub.Model.Enums;
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
    List<PropertyFileDto>? Files = null);
