using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;

namespace HousingHub.Service.InspectionService.Interfaces;

public interface IInspectionCommandService
{
    Task<BaseResponse<InspectionDto>> ScheduleInspectionAsync(ScheduleInspectionDto request, Guid authenticatedUserId);
    Task<BaseResponse<InspectionDto>> RespondToInspectionAsync(RespondToInspectionDto request, Guid authenticatedUserId, bool isAdminAction = false);
    Task<BaseResponse<InspectionDto>> RescheduleInspectionAsync(RescheduleInspectionDto request, Guid authenticatedUserId);
    Task<BaseResponse<InspectionDto>> RespondToRescheduleAsync(Guid inspectionId, bool accept, Guid authenticatedUserId, string? note = null);
    Task<BaseResponse<bool>> CancelInspectionAsync(Guid inspectionId, Guid authenticatedUserId, bool isAdminAction = false);

    /// <summary>
    /// Sends a 24-hour reminder (email + an automated Admin chat message) to both
    /// the owner and the customer for every confirmed inspection due within the
    /// next 24 hours that hasn't been reminded yet. Meant to be invoked by a
    /// scheduled trigger, not directly by a user. Returns the number of
    /// inspections reminded.
    /// </summary>
    Task<BaseResponse<int>> SendDueInspectionRemindersAsync();
}
