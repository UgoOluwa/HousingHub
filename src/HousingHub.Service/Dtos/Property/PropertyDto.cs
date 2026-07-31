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
    double? Latitude,
    double? Longitude,
    long ViewCount,
    bool IsPublished,
    DateTime? PublishedAt,
    bool IsVerified,
    DateTime? VerifiedAt,
    List<PropertyFileDto>? Files = null,
    // Count of open (Pending or Rescheduled) inspection requests for this property.
    int InspectionCount = 0);
