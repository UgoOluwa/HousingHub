using HousingHub.Application.Commons.Bases;
using HousingHub.Service.NotificationService.Interfaces;
using MediatR;

namespace HousingHub.Application.Notification.Queries.GetUnreadCount;

public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, BaseResponse<int>>
{
    private readonly INotificationQueryService _notificationQueryService;

    public GetUnreadCountQueryHandler(INotificationQueryService notificationQueryService)
    {
        _notificationQueryService = notificationQueryService;
    }

    public async Task<BaseResponse<int>> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var response = await _notificationQueryService.GetUnreadCountAsync(request.RecipientId);
        return new BaseResponse<int>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
