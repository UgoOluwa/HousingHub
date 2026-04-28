namespace HousingHub.Service.Dtos.Admin;

public record AdminCustomerListDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateTime DateJoined,
    bool IsActive,
    bool IsKycVerified,
    bool KycPending,
    int CustomerType,
    int PendingInspections);
