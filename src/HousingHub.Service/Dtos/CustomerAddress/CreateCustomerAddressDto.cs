namespace HousingHub.Service.Dtos.CustomerAddress;

public record CreateCustomerAddressDto(string Street, string City, string State, string Country, string PostalCode, Guid CustomerId);
