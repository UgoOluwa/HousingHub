using HousingHub.Application.Commons.Bases;
using HousingHub.Service.NotificationService.Interfaces;
using MediatR;

namespace HousingHub.Application.Notification.Commands.MarkAllAsRead;

public class MarkAllNotificationsAsReadCommandHandler : IRequestHandler<MarkAllNotificationsAsReadCommand, BaseResponse<bool>>
{
    private readonly INotificationCommandService _notificationCommandService;

    public MarkAllNotificationsAsReadCommandHandler(INotificationCommandService notificationCommandService)
    {
        _notificationCommandService = notificationCommandService;
    }

    public async Task<BaseResponse<bool>> Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        var response = await _notificationCommandService.MarkAllAsReadAsync(request.AuthenticatedUserId);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
