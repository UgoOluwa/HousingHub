using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Chat.Commands.MarkAsRead;

public record MarkConversationAsReadCommand(Guid ConversationId, Guid AuthenticatedUserId) : IRequest<BaseResponse<bool>>;
