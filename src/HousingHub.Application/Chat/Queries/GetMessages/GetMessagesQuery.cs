using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Chat;
using MediatR;

namespace HousingHub.Application.Chat.Queries.GetMessages;

public record GetMessagesQuery(Guid ConversationId, Guid AuthenticatedUserId, int PageNumber = 1, int PageSize = 20)
    : IRequest<BaseResponsePagination<PaginatedResult<ChatMessageDto>>>;
