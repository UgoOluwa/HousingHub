namespace HousingHub.Service.Dtos.Admin;

public record AdminStaffDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateTime DateJoined,
    bool IsActive);
