using HousingHub.Service.Dtos.PropertyAddress;
using HousingHub.Service.Dtos.PropertyFile;

namespace HousingHub.Service.Dtos.Property;

public record CreatePropertyDto(string Title, string Description, int PropertyType, decimal Price, bool IsAvailable, int PropertyLeaseType, Guid OwnerId, CreatePropertyAddressDto? propertyAddress, CreatePropertyFileDto? propertyFile);
