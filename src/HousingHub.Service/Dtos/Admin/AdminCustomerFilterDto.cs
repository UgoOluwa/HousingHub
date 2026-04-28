namespace HousingHub.Service.Dtos.Admin;

public record AdminCustomerFilterDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsVerified = null,
    bool? IsActive = null);
