using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Chat;
using MediatR;

namespace HousingHub.Application.Chat.Commands.SendMessage;

public record SendMessageCommand(Guid RecipientId, string Content, Guid SenderId) : IRequest<BaseResponse<ChatMessageDto>>;
