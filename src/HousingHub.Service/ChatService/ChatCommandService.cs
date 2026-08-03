using Amazon.DynamoDBv2.DataModel;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Chat;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.NotificationService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.ChatService;

public class ChatCommandService : IChatCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<ChatCommandService> _logger;
    private readonly IChatRealtimeNotifier _realtimeNotifier;
    private readonly IRealtimeNotifier _bellRealtimeNotifier;
    private readonly IEmailService _emailService;
    private readonly IDynamoDBContext _dynamoDb;

    public ChatCommandService(
        IUnitOfWOrk unitOfWOrk,
        ILogger<ChatCommandService> logger,
        IChatRealtimeNotifier realtimeNotifier,
        IRealtimeNotifier bellRealtimeNotifier,
        IEmailService emailService,
        IDynamoDBContext dynamoDb)
    {
        _unitOfWOrk = unitOfWOrk;
        _logger = logger;
        _realtimeNotifier = realtimeNotifier;
        _bellRealtimeNotifier = bellRealtimeNotifier;
        _emailService = emailService;
        _dynamoDb = dynamoDb;
    }

    public async Task<BaseResponse<ChatMessageDto>> SendMessageAsync(SendMessageDto request, Guid senderId)
    {
        try
        {
            if (senderId == request.RecipientId)
                return new BaseResponse<ChatMessageDto>(null, false, string.Empty, "You cannot send a message to yourself.");

            // Sender is usually a Customer, but an admin messaging a customer/owner
            // from the Admin dashboard is also a valid sender — falls back to the
            // Admins table when not found among customers.
            var senderName = await ResolveSenderNameAsync(senderId);
            if (senderName == null)
                return new BaseResponse<ChatMessageDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("sender"));

            // Recipient is usually a Customer, but a customer/owner messaging an
            // admin (or replying to one) is also valid — falls back to the Admins
            // table when not found among customers, same as ResolveSenderNameAsync.
            var recipientInfo = await ResolveRecipientInfoAsync(request.RecipientId);
            if (recipientInfo == null)
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

            var notification = new Notification(
                request.RecipientId,
                null,
                NotificationType.NewMessage,
                $"New message from {senderName}",
                conversation.LastMessage);
            await _unitOfWOrk.NotificationCommands.InsertAsync(notification);

            await _unitOfWOrk.SaveAsync();

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

            var notificationDto = new NotificationDto(
                notification.Id,
                notification.DateCreated,
                notification.RecipientId,
                notification.InspectionId,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.IsRead);
            await _bellRealtimeNotifier.SendNotificationAsync(request.RecipientId, notificationDto);

            var conversationUpdate = new ConversationDto(
                conversation.Id,
                senderId,
                senderName,
                conversation.LastMessage,
                conversation.LastMessageAt,
                0);
            await _realtimeNotifier.NotifyConversationUpdatedAsync(request.RecipientId, conversationUpdate);

            await SafeSendNewMessageEmailAsync(recipientInfo.Value.Email, recipientInfo.Value.FirstName, senderName, conversation.LastMessage);

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

    private async Task<string?> ResolveSenderNameAsync(Guid senderId)
    {
        var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == senderId);
        if (customer != null) return $"{customer.FirstName} {customer.LastName}";

        var admin = await _dynamoDb.LoadAsync<Admin>(senderId);
        return admin != null ? $"Admin - {admin.FirstName} {admin.LastName}" : null;
    }

    private async Task<(string Email, string FirstName)?> ResolveRecipientInfoAsync(Guid recipientId)
    {
        var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == recipientId);
        if (customer != null) return (customer.Email, customer.FirstName);

        var admin = await _dynamoDb.LoadAsync<Admin>(recipientId);
        return admin != null ? (admin.Email, admin.FirstName) : null;
    }

    private async Task<Conversation?> FindConversationAsync(Guid userOneId, Guid userTwoId)
    {
        return await _unitOfWOrk.ConversationQueries.GetByAsync(
            c => (c.ParticipantOneId == userOneId && c.ParticipantTwoId == userTwoId) ||
                 (c.ParticipantOneId == userTwoId && c.ParticipantTwoId == userOneId));
    }

    private async Task SafeSendNewMessageEmailAsync(string recipientEmail, string recipientFirstName, string senderName, string messagePreview)
    {
        try
        {
            await _emailService.SendNewMessageAsync(recipientEmail, recipientFirstName, senderName, messagePreview);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send new-message email to {Email}", recipientEmail);
        }
    }
}
