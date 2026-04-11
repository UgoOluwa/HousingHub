namespace HousingHub.Service.Dtos.Chat;

public record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string Content,
    bool IsRead,
    DateTime DateCreated);
