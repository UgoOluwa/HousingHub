namespace HousingHub.Service.Dtos.Chat;

public record SendMessageDto(Guid RecipientId, string Content);
