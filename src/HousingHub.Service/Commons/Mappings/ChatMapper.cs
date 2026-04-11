using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Chat;

namespace HousingHub.Service.Commons.Mappings;

public class ChatMapper : Profile
{
    public ChatMapper()
    {
        CreateMap<ChatMessage, ChatMessageDto>()
            .ForMember(dest => dest.SenderName, opt => opt.Ignore());
    }
}
