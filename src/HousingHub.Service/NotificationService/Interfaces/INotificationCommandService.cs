using HousingHub.Core.CustomResponses;

namespace HousingHub.Service.NotificationService.Interfaces;

public interface INotificationCommandService
{
    Task<BaseResponse<bool>> MarkAsReadAsync(Guid notificationId, Guid authenticatedUserId);
    Task<BaseResponse<bool>> MarkAllAsReadAsync(Guid authenticatedUserId);
}
