using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Dtos.Chat;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.ChatService;

public class ChatQueryService : IChatQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<ChatQueryService> _logger;

    public ChatQueryService(IUnitOfWOrk unitOfWOrk, ILogger<ChatQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _logger = logger;
    }

    public async Task<BaseResponse<List<ConversationDto>>> GetConversationsAsync(Guid userId)
    {
        try
        {
            var conversations = await _unitOfWOrk.ConversationQueries.GetAllAsync(
                c => c.ParticipantOneId == userId || c.ParticipantTwoId == userId);

            var conversationList = conversations.ToList();

            // Gather all other participant IDs
            var otherParticipantIds = conversationList
                .Select(c => c.GetOtherParticipantId(userId))
                .Distinct()
                .ToList();

            // Fetch participant details in bulk
            var participants = await _unitOfWOrk.CustomerQueries.GetAllAsync(
                c => otherParticipantIds.Contains(c.Id));
            var participantMap = participants.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

            // Fetch unread counts per conversation
            var allMessages = await _unitOfWOrk.ChatMessageQueries.GetAllAsync(
                m => m.SenderId != userId && !m.IsRead);
            var unreadByConversation = allMessages
                .GroupBy(m => m.ConversationId)
                .ToDictionary(g => g.Key, g => g.Count());

            var result = conversationList
                .OrderByDescending(c => c.LastMessageAt ?? c.DateCreated)
                .Select(c =>
                {
                    var otherParticipantId = c.GetOtherParticipantId(userId);
                    participantMap.TryGetValue(otherParticipantId, out var participantName);
                    unreadByConversation.TryGetValue(c.Id, out var unreadCount);

                    return new ConversationDto(
                        c.Id,
                        otherParticipantId,
                        participantName ?? "Unknown User",
                        c.LastMessage,
                        c.LastMessageAt,
                        unreadCount);
                })
                .ToList();

            return new BaseResponse<List<ConversationDto>>(result, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetConversationsAsync: {Message}", ex.Message);
            return new BaseResponse<List<ConversationDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<ChatMessageDto>>> GetMessagesAsync(
        Guid conversationId, Guid authenticatedUserId, int pageNumber, int pageSize)
    {
        try
        {
            var conversation = await _unitOfWOrk.ConversationQueries.GetByAsync(x => x.Id == conversationId);
            if (conversation == null)
                return new BaseResponse<PaginatedResult<ChatMessageDto>>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("conversation"));

            if (!conversation.HasParticipant(authenticatedUserId))
                return new BaseResponse<PaginatedResult<ChatMessageDto>>(null, false, string.Empty, "You are not a participant in this conversation.");

            // Get all messages for this conversation, then paginate from the most recent
            var allMessages = (await _unitOfWOrk.ChatMessageQueries.GetAllAsync(
                m => m.ConversationId == conversationId)).ToList();

            var totalCount = allMessages.Count;
            var pagedMessages = allMessages
                .OrderByDescending(m => m.DateCreated)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Fetch sender names
            var senderIds = pagedMessages.Select(m => m.SenderId).Distinct().ToList();
            var senders = await _unitOfWOrk.CustomerQueries.GetAllAsync(c => senderIds.Contains(c.Id));
            var senderMap = senders.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

            var messageDtos = pagedMessages
                .Select(m => new ChatMessageDto(
                    m.Id,
                    m.ConversationId,
                    m.SenderId,
                    senderMap.GetValueOrDefault(m.SenderId, "Unknown User"),
                    m.Content,
                    m.IsRead,
                    m.DateCreated))
                .ToList();

            var paginatedResult = new PaginatedResult<ChatMessageDto>(messageDtos, totalCount, pageNumber, pageSize);

            return new BaseResponse<PaginatedResult<ChatMessageDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetMessagesAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<ChatMessageDto>>(null, false, string.Empty, ex.Message);
        }
    }
}
