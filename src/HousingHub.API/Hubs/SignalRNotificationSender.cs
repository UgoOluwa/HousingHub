using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.NotificationService.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace HousingHub.API.Hubs;

public class SignalRNotificationSender : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationSender(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendNotificationAsync(Guid recipientId, NotificationDto notification)
    {
        await _hubContext.Clients.User(recipientId.ToString())
            .SendAsync("ReceiveNotification", notification);
    }
}
