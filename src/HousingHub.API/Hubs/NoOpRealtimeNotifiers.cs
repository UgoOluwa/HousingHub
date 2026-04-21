using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Dtos.Chat;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.NotificationService.Interfaces;

namespace HousingHub.API.Hubs;

public class NoOpRealtimeNotifier : IRealtimeNotifier
{
    public Task SendNotificationAsync(Guid recipientId, NotificationDto notification) => Task.CompletedTask;
}

public class NoOpChatRealtimeNotifier : IChatRealtimeNotifier
{
    public Task SendMessageAsync(Guid recipientId, ChatMessageDto message) => Task.CompletedTask;
    public Task NotifyConversationUpdatedAsync(Guid recipientId, ConversationDto conversation) => Task.CompletedTask;
    public Task NotifyMessagesReadAsync(Guid recipientId, Guid conversationId) => Task.CompletedTask;
    public Task NotifyTypingAsync(Guid recipientId, Guid conversationId, Guid senderId, string senderName) => Task.CompletedTask;
}
