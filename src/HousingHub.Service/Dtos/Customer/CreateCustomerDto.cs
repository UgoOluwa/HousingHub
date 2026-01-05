using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.CustomerAddress;

namespace HousingHub.Service.Dtos.Customer;

public record CreateCustomerDto(string FirstName, string LastName, string Email, string PhoneNumber, CustomerType CustomerType, DateTime? DateOfBirth);

public record CreateCustomerDtoWithDetails(string FirstName, string LastName, string Email, string PhoneNumber, CustomerType CustomerType, DateTime? DateOfBirth, string? NationalIdNumber, string? IdDocumentUrl, DateTime? KycSubmittedAt, bool IsKycVerified, string? JobTitle, string? CompanyName, string? Industry, CreateCustomerAddressDto? Address);
