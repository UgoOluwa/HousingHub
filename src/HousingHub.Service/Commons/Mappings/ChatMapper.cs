using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Chat;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class ChatMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ChatMessage, ChatMessageDto>()
            .Ignore(dest => dest.SenderName);
    }
}
