using HousingHub.Service.Dtos.CustomerAddress;

namespace HousingHub.Service.Dtos.Customer;

public record CustomerDto(Guid Id, DateTime DateCreated, DateTime DateModified, string FirstName, string LastName, string Email, string PhoneNumber, int CustomerType, DateTime? DateOfBirth);

public record CustomerWithDetailsDto(Guid Id, DateTime DateCreated, DateTime DateModified, string FirstName, string LastName, string Email, string PhoneNumber, int CustomerType, DateTime? DateOfBirth, string? NationalIdNumber, string? IdDocumentUrl, DateTime? KycSubmittedAt, bool IsKycVerified, string? JobTitle, string? CompanyName, string? Industry, CustomerAddressDto Address);
