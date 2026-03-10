using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.NotificationService.Interfaces;
using MediatR;

namespace HousingHub.Application.Notification.Queries.GetAll;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, BaseResponsePagination<PaginatedResult<NotificationDto>>>
{
    private readonly INotificationQueryService _notificationQueryService;

    public GetNotificationsQueryHandler(INotificationQueryService notificationQueryService)
    {
        _notificationQueryService = notificationQueryService;
    }

    public async Task<BaseResponsePagination<PaginatedResult<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var response = await _notificationQueryService.GetNotificationsAsync(request.RecipientId, request.PageNumber, request.PageSize, request.UnreadOnly);
        var paginatedResponse = new BaseResponsePagination<PaginatedResult<NotificationDto>>(
            response.IsSuccessful, response.Data, response.Message, null);

        if (response.Data != null)
        {
            paginatedResponse.PageNumber = response.Data.PageNumber;
            paginatedResponse.TotalPages = response.Data.TotalPages;
            paginatedResponse.TotalCount = response.Data.TotalCount;
        }

        return paginatedResponse;
    }
}
