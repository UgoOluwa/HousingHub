namespace HousingHub.Service.Dtos.CustomerAddress;

public record CustomerAddressDto(Guid Id, DateTime DateCreated, DateTime DateModified, string Street, string City, string State, string Country, string PostalCode, Guid CustomerId);
