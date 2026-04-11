using HousingHub.Application.Commons.Bases;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Dtos.Chat;
using MediatR;

namespace HousingHub.Application.Chat.Queries.GetConversations;

public class GetConversationsQueryHandler : IRequestHandler<GetConversationsQuery, BaseResponse<List<ConversationDto>>>
{
    private readonly IChatQueryService _chatQueryService;

    public GetConversationsQueryHandler(IChatQueryService chatQueryService)
    {
        _chatQueryService = chatQueryService;
    }

    public async Task<BaseResponse<List<ConversationDto>>> Handle(GetConversationsQuery request, CancellationToken cancellationToken)
    {
        var response = await _chatQueryService.GetConversationsAsync(request.UserId);
        return new BaseResponse<List<ConversationDto>>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
