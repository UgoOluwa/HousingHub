using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Dtos.Chat;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.ChatService;

public class ChatCommandService : IChatCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<ChatCommandService> _logger;
    private readonly IChatRealtimeNotifier _realtimeNotifier;

    public ChatCommandService(IUnitOfWOrk unitOfWOrk, ILogger<ChatCommandService> logger, IChatRealtimeNotifier realtimeNotifier)
    {
        _unitOfWOrk = unitOfWOrk;
        _logger = logger;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<BaseResponse<ChatMessageDto>> SendMessageAsync(SendMessageDto request, Guid senderId)
    {
        try
        {
            if (senderId == request.RecipientId)
                return new BaseResponse<ChatMessageDto>(null, false, string.Empty, "You cannot send a message to yourself.");

            var sender = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == senderId);
            if (sender == null)
                return new BaseResponse<ChatMessageDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("sender"));

            var recipient = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == request.RecipientId);
            if (recipient == null)
                return new BaseResponse<ChatMessageDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("recipient"));

            // Find or create conversation
            var conversation = await FindConversationAsync(senderId, request.RecipientId);
            if (conversation == null)
            {
                conversation = new Conversation(senderId, request.RecipientId);
                await _unitOfWOrk.ConversationCommands.InsertAsync(conversation);
            }

            // Create message
            var message = new ChatMessage(conversation.Id, senderId, request.Content);
            await _unitOfWOrk.ChatMessageCommands.InsertAsync(message);

            // Update conversation preview
            conversation.LastMessage = request.Content.Length > 100
                ? request.Content[..100] + "..."
                : request.Content;
            conversation.LastMessageAt = message.DateCreated;
            await _unitOfWOrk.ConversationCommands.UpdateAsync(conversation);

            await _unitOfWOrk.SaveAsync();

            string senderName = $"{sender.FirstName} {sender.LastName}";
            var dto = new ChatMessageDto(
                message.Id,
                message.ConversationId,
                message.SenderId,
                senderName,
                message.Content,
                message.IsRead,
                message.DateCreated);

            // Push real-time notification to the recipient
            await _realtimeNotifier.SendMessageAsync(request.RecipientId, dto);

            var conversationUpdate = new ConversationDto(
                conversation.Id,
                senderId,
                senderName,
                conversation.LastMessage,
                conversation.LastMessageAt,
                0);
            await _realtimeNotifier.NotifyConversationUpdatedAsync(request.RecipientId, conversationUpdate);

            return new BaseResponse<ChatMessageDto>(dto, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in SendMessageAsync: {Message}", ex.Message);
            return new BaseResponse<ChatMessageDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> MarkConversationAsReadAsync(Guid conversationId, Guid authenticatedUserId)
    {
        try
        {
            var conversation = await _unitOfWOrk.ConversationQueries.GetByAsync(x => x.Id == conversationId);
            if (conversation == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("conversation"));

            if (!conversation.HasParticipant(authenticatedUserId))
                return new BaseResponse<bool>(false, false, string.Empty, "You are not a participant in this conversation.");

            var unreadMessages = await _unitOfWOrk.ChatMessageQueries.GetAllAsync(
                x => x.ConversationId == conversationId && x.SenderId != authenticatedUserId && !x.IsRead);

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
            }

            await _unitOfWOrk.ChatMessageCommands.UpdateRangeAsync(unreadMessages);
            await _unitOfWOrk.SaveAsync();

            // Notify the other participant that their messages have been read
            var otherParticipantId = conversation.GetOtherParticipantId(authenticatedUserId);
            await _realtimeNotifier.NotifyMessagesReadAsync(otherParticipantId, conversationId);

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in MarkConversationAsReadAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }

    private async Task<Conversation?> FindConversationAsync(Guid userOneId, Guid userTwoId)
    {
        return await _unitOfWOrk.ConversationQueries.GetByAsync(
            c => (c.ParticipantOneId == userOneId && c.ParticipantTwoId == userTwoId) ||
                 (c.ParticipantOneId == userTwoId && c.ParticipantTwoId == userOneId));
    }
}
