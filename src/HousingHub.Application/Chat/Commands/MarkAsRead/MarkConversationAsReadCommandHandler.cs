using HousingHub.Application.Commons.Bases;
using HousingHub.Service.ChatService.Interfaces;
using MediatR;

namespace HousingHub.Application.Chat.Commands.MarkAsRead;

public class MarkConversationAsReadCommandHandler : IRequestHandler<MarkConversationAsReadCommand, BaseResponse<bool>>
{
    private readonly IChatCommandService _chatCommandService;

    public MarkConversationAsReadCommandHandler(IChatCommandService chatCommandService)
    {
        _chatCommandService = chatCommandService;
    }

    public async Task<BaseResponse<bool>> Handle(MarkConversationAsReadCommand request, CancellationToken cancellationToken)
    {
        var response = await _chatCommandService.MarkConversationAsReadAsync(request.ConversationId, request.AuthenticatedUserId);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
