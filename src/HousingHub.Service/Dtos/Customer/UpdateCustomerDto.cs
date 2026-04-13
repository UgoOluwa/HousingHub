using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.CustomerAddress;

namespace HousingHub.Service.Dtos.Customer;

public record UpdateCustomerDto(Guid Id, string FirstName, string LastName, string Email, string PhoneNumber, CustomerType CustomerType, DateTime? DateOfBirth, string? NationalIdNumber, string? IdDocumentUrl, DateTime? KycSubmittedAt, bool? IsKycVerified, string? JobTitle, string? CompanyName, string? Industry, UpdateCustomerAddressDto? Address);

public record UpdateProfileDto(string FirstName, string LastName, string PhoneNumber, DateTime? DateOfBirth, string? JobTitle, string? CompanyName, string? Industry);

public record SubmitKycDto(DateTime? DateOfBirth, string NationalIdNumber, IDType IdType, string? IdDocumentUrl, string? JobTitle, string? CompanyName, string? Industry);
