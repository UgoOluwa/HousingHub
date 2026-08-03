using HousingHub.Service.Dtos.CustomerAddress;

namespace HousingHub.Service.Dtos.Customer;

public record CustomerDto(Guid Id, DateTime DateCreated, DateTime DateModified, string FirstName, string LastName, string Email, string PhoneNumber, int CustomerType, DateTime? DateOfBirth, DateTime? KycSubmittedAt = null, bool IsKycVerified = false);

public record LoginCustomerDto(string EmailOrPhone, string Password);

public record LoginCustomerResponseDto(Guid Id, DateTime DateCreated, string FirstName, string LastName, string Email, string PhoneNumber, int CustomerType, string token, string? refreshToken = null);

public record CustomerWithDetailsDto(Guid Id, DateTime DateCreated, DateTime DateModified, string FirstName, string LastName, string Email, string PhoneNumber, int CustomerType, DateTime? DateOfBirth, string? NationalIdNumber, string? IdDocumentUrl, DateTime? KycSubmittedAt, bool IsKycVerified, bool EmailVerified, string? ProfileImageUrl, string? JobTitle, string? CompanyName, string? Industry, CustomerAddressDto Address, string? KycRejectionReason, bool IsManagedByHousingHub = false);
