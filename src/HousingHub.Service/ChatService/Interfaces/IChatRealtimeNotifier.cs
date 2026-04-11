using HousingHub.Service.Dtos.Chat;

namespace HousingHub.Service.ChatService.Interfaces;

public interface IChatRealtimeNotifier
{
    Task SendMessageAsync(Guid recipientId, ChatMessageDto message);
    Task NotifyConversationUpdatedAsync(Guid recipientId, ConversationDto conversation);
    Task NotifyMessagesReadAsync(Guid recipientId, Guid conversationId);
    Task NotifyTypingAsync(Guid recipientId, Guid conversationId, Guid senderId, string senderName);
}
