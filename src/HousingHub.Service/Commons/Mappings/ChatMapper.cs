using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Chat;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class ChatMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // SenderName is a constructor parameter of a positional record, so it cannot be
        // Ignore()d — Mapster would emit a 6-argument call to a 7-argument constructor
        // and fail to compile ("Incorrect number of arguments for constructor").
        //
        // Because TypeAdapterConfig.GlobalSettings is shared, that one bad registration
        // broke mapping across the whole app, surfacing as a 500 ("The type initializer
        // for 'Mapster.TypeAdapter`2' threw an exception") on unrelated operations such
        // as publishing a property.
        //
        // Sender is [DynamoDBIgnore] so it is never loaded; map it to a known-safe value
        // and let the chat service fill in the display name.
        config.NewConfig<ChatMessage, ChatMessageDto>()
            .Map(dest => dest.SenderName, src => string.Empty);
    }
}
