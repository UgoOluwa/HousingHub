using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Dtos.Chat;
using Microsoft.AspNetCore.SignalR;

namespace HousingHub.API.Hubs;

public class SignalRChatNotifier : IChatRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRChatNotifier(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendMessageAsync(Guid recipientId, ChatMessageDto message)
    {
        await _hubContext.Clients.User(recipientId.ToString())
            .SendAsync("ReceiveMessage", message);

        await _hubContext.Clients.Group(message.ConversationId.ToString())
            .SendAsync("NewMessageInConversation", message);
    }

    public async Task NotifyConversationUpdatedAsync(Guid recipientId, ConversationDto conversation)
    {
        await _hubContext.Clients.User(recipientId.ToString())
            .SendAsync("ConversationUpdated", conversation);
    }

    public async Task NotifyMessagesReadAsync(Guid recipientId, Guid conversationId)
    {
        await _hubContext.Clients.User(recipientId.ToString())
            .SendAsync("MessagesRead", conversationId);
    }

    public async Task NotifyTypingAsync(Guid recipientId, Guid conversationId, Guid senderId, string senderName)
    {
        await _hubContext.Clients.User(recipientId.ToString())
            .SendAsync("UserTyping", conversationId, senderId, senderName);
    }
}
