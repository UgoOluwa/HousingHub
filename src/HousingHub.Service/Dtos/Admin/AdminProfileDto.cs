namespace HousingHub.Service.Dtos.Admin;

public record AdminProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateTime DateCreated,
    bool IsActive);
