using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Inspection;

namespace HousingHub.Service.InspectionService.Interfaces;

public interface IInspectionQueryService
{
    /// <summary>Only the inspection's customer or the property's owner may view it.</summary>
    Task<BaseResponse<InspectionDto?>> GetInspectionAsync(Guid id, Guid authenticatedUserId);

    /// <summary>Admin: view any inspection, bypassing the participant check.</summary>
    Task<BaseResponse<InspectionDto?>> GetInspectionAsync(Guid id);
    /// <summary>
    /// Inspections booked against a property. <paramref name="requestingUserId"/> must be
    /// the property's owner; anyone else receives an empty, unsuccessful response.
    /// </summary>
    Task<BaseResponse<PaginatedResult<InspectionDto>>> GetInspectionsByPropertyAsync(Guid propertyId, Guid requestingUserId, int pageNumber, int pageSize, InspectionStatus? status = null);
    Task<BaseResponse<PaginatedResult<InspectionDto>>> GetInspectionsByCustomerAsync(Guid customerId, int pageNumber, int pageSize, InspectionStatus? status = null);
    Task<BaseResponse<PaginatedResult<OwnerInspectionDto>>> GetInspectionsByOwnerAsync(Guid ownerId, int pageNumber, int pageSize, InspectionStatus? status = null);

    /// <summary>Admin: paginated list of all inspections across the platform with enriched details.</summary>
    Task<BaseResponse<PaginatedResult<AdminInspectionListDto>>> GetAllInspectionsPaginatedAsync(AdminInspectionFilterDto filter);

    /// <summary>Admin: paginated list of inspections scheduled for today with enriched property/customer info.</summary>
    Task<BaseResponse<PaginatedResult<AdminTodayInspectionDto>>> GetTodaysInspectionsPaginatedAsync(int pageNumber, int pageSize);

    /// <summary>Admin: recent platform activity feed (new users, KYC submissions, new properties, new inspections).</summary>
    Task<BaseResponse<List<AdminRecentActivityDto>>> GetRecentActivityAsync(int count = 20, int days = 7);
}
