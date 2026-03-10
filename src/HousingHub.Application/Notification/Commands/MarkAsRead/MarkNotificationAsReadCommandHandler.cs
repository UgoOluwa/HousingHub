using HousingHub.Application.Commons.Bases;
using HousingHub.Service.NotificationService.Interfaces;
using MediatR;

namespace HousingHub.Application.Notification.Commands.MarkAsRead;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, BaseResponse<bool>>
{
    private readonly INotificationCommandService _notificationCommandService;

    public MarkNotificationAsReadCommandHandler(INotificationCommandService notificationCommandService)
    {
        _notificationCommandService = notificationCommandService;
    }

    public async Task<BaseResponse<bool>> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var response = await _notificationCommandService.MarkAsReadAsync(request.NotificationId, request.AuthenticatedUserId);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
