using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Chat;

namespace HousingHub.Service.ChatService.Interfaces;

public interface IChatQueryService
{
    Task<BaseResponse<List<ConversationDto>>> GetConversationsAsync(Guid userId);
    Task<BaseResponse<PaginatedResult<ChatMessageDto>>> GetMessagesAsync(Guid conversationId, Guid authenticatedUserId, int pageNumber, int pageSize);
}
