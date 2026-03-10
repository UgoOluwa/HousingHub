using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Notification;

namespace HousingHub.Service.NotificationService.Interfaces;

public interface INotificationQueryService
{
    Task<BaseResponse<PaginatedResult<NotificationDto>>> GetNotificationsAsync(Guid recipientId, int pageNumber, int pageSize, bool? unreadOnly = null);
    Task<BaseResponse<int>> GetUnreadCountAsync(Guid recipientId);
}
