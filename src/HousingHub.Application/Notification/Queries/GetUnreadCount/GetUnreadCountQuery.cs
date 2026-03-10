using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Notification.Queries.GetUnreadCount;

public record GetUnreadCountQuery(Guid RecipientId) : IRequest<BaseResponse<int>>;
