using HousingHub.Application.Commons.Bases;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Dtos.Chat;
using MediatR;

namespace HousingHub.Application.Chat.Commands.SendMessage;

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, BaseResponse<ChatMessageDto>>
{
    private readonly IChatCommandService _chatCommandService;

    public SendMessageCommandHandler(IChatCommandService chatCommandService)
    {
        _chatCommandService = chatCommandService;
    }

    public async Task<BaseResponse<ChatMessageDto>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var response = await _chatCommandService.SendMessageAsync(
            new SendMessageDto(request.RecipientId, request.Content), request.SenderId);
        return new BaseResponse<ChatMessageDto>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
