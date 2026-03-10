using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Notification.Commands.MarkAllAsRead;

public record MarkAllNotificationsAsReadCommand(Guid AuthenticatedUserId) : IRequest<BaseResponse<bool>>;
