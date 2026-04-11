using HousingHub.Service.Dtos.Notification;

namespace HousingHub.Service.NotificationService.Interfaces;

public interface IRealtimeNotifier
{
    Task SendNotificationAsync(Guid recipientId, NotificationDto notification);
}
