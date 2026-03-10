using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Notification;
using MediatR;

namespace HousingHub.Application.Notification.Queries.GetAll;

public record GetNotificationsQuery(
    Guid RecipientId,
    int PageNumber = 1,
    int PageSize = 10,
    bool? UnreadOnly = null) : IRequest<BaseResponsePagination<PaginatedResult<NotificationDto>>>;
