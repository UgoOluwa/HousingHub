using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;

namespace HousingHub.Service.InspectionService.Interfaces;

public interface IInspectionCommandService
{
    Task<BaseResponse<InspectionDto>> ScheduleInspectionAsync(ScheduleInspectionDto request, Guid authenticatedUserId);
    Task<BaseResponse<InspectionDto>> RespondToInspectionAsync(RespondToInspectionDto request, Guid authenticatedUserId);
    Task<BaseResponse<InspectionDto>> RescheduleInspectionAsync(RescheduleInspectionDto request, Guid authenticatedUserId);
    Task<BaseResponse<InspectionDto>> RespondToRescheduleAsync(Guid inspectionId, bool accept, Guid authenticatedUserId);
    Task<BaseResponse<bool>> CancelInspectionAsync(Guid inspectionId, Guid authenticatedUserId);
}
