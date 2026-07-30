namespace HousingHub.Service.Dtos.PropertyAddress;

// The client doesn't need to know the underlying PropertyAddress record's own id —
// the handler resolves it from the property's AddressId.
public record UpdatePropertyAddressDto(string? Place, string? City, string? State, string? Country, string? PostalCode);
