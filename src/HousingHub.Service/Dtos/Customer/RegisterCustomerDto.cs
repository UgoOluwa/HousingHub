using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Customer;

public record RegisterCustomerDto(string FirstName, string LastName, string Email, string PhoneNumber, string Password, CustomerType CustomerType);
