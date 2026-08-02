namespace HousingHub.Service.Dtos.Admin;

public record AdminLoginResultDto(Guid Id, string Token, string FirstName, string LastName, string Email, string? RefreshToken = null, string Role = HousingHub.Model.Entities.AdminRoles.Admin);
