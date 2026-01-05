namespace HousingHub.Service.Dtos.PropertyAddress;

public record UpdatePropertyAddressDto(Guid Id, string Place, string City, string State, string Country, string PostalCode);
