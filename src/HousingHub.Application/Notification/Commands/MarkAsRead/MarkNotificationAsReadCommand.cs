using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Notification.Commands.MarkAsRead;

public record MarkNotificationAsReadCommand(Guid NotificationId, Guid AuthenticatedUserId) : IRequest<BaseResponse<bool>>;
