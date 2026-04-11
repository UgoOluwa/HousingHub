using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Chat;
using MediatR;

namespace HousingHub.Application.Chat.Queries.GetConversations;

public record GetConversationsQuery(Guid UserId) : IRequest<BaseResponse<List<ConversationDto>>>;
