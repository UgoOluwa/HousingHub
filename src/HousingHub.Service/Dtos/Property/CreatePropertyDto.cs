using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.PropertyAddress;
using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.Dtos.Property;

public record CreatePropertyDto(
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
    CreatePropertyAddressDto? PropertyAddress,
    IList<IFormFile>? Files = null);
