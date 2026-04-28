using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Admin;

public record AdminInspectionFilterDto(
    int PageNumber = 1,
    int PageSize = 20,
    InspectionStatus? Status = null,
    DateTime? Date = null,
    Guid? PropertyId = null,
    Guid? CustomerId = null);
