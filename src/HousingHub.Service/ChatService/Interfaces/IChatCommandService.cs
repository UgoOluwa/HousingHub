using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Chat;

namespace HousingHub.Service.ChatService.Interfaces;

public interface IChatCommandService
{
    Task<BaseResponse<ChatMessageDto>> SendMessageAsync(SendMessageDto request, Guid senderId);
    Task<BaseResponse<bool>> MarkConversationAsReadAsync(Guid conversationId, Guid authenticatedUserId);
}
