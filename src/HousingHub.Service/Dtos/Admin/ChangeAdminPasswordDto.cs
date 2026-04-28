namespace HousingHub.Service.Dtos.Admin;

public record ChangeAdminPasswordDto(string CurrentPassword, string NewPassword);
