using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Dtos.Chat;
using MediatR;

namespace HousingHub.Application.Chat.Queries.GetMessages;

public class GetMessagesQueryHandler : IRequestHandler<GetMessagesQuery, BaseResponsePagination<PaginatedResult<ChatMessageDto>>>
{
    private readonly IChatQueryService _chatQueryService;

    public GetMessagesQueryHandler(IChatQueryService chatQueryService)
    {
        _chatQueryService = chatQueryService;
    }

    public async Task<BaseResponsePagination<PaginatedResult<ChatMessageDto>>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
    {
        var response = await _chatQueryService.GetMessagesAsync(
            request.ConversationId, request.AuthenticatedUserId, request.PageNumber, request.PageSize);

        var paginatedResponse = new BaseResponsePagination<PaginatedResult<ChatMessageDto>>(
            response.IsSuccessful, response.Data, response.Message, null);

        if (response.Data != null)
        {
            paginatedResponse.PageNumber = response.Data.PageNumber;
            paginatedResponse.TotalPages = response.Data.TotalPages;
            paginatedResponse.TotalCount = response.Data.TotalCount;
        }

        return paginatedResponse;
    }
}
