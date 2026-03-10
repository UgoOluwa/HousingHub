using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;

namespace HousingHub.Service.InspectionService.Interfaces;

public interface IInspectionQueryService
{
    Task<BaseResponse<InspectionDto?>> GetInspectionAsync(Guid id);
    Task<BaseResponse<PaginatedResult<InspectionDto>>> GetInspectionsByPropertyAsync(Guid propertyId, int pageNumber, int pageSize, InspectionStatus? status = null);
    Task<BaseResponse<PaginatedResult<InspectionDto>>> GetInspectionsByCustomerAsync(Guid customerId, int pageNumber, int pageSize, InspectionStatus? status = null);
}
