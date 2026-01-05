namespace HousingHub.Service.Dtos.PropertyAddress;

public record PropertyAddressDto(Guid Id, DateTime DateCreated, DateTime DateModified, string Place, string City, string State, string Country, string PostalCode, Guid PropertyId);
