namespace HousingHub.Service.Dtos.PropertyAddress;

public record CreatePropertyAddressDto(string Place, string City, string State, string Country, string PostalCode, Guid PropertyId);
