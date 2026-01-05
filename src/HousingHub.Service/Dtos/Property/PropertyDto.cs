namespace HousingHub.Service.Dtos.Property;

public record PropertyDto(Guid Id, DateTime DateCreated, DateTime DateModified, string Title, string Description, int PropertyType, decimal Price, bool IsAvailable, int PropertyLeaseType, Guid OwnerId, Guid AddressId);
