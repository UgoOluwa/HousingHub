namespace HousingHub.Service.Dtos.Chat;

public record ConversationDto(
    Guid Id,
    Guid ParticipantId,
    string ParticipantName,
    string? LastMessage,
    DateTime? LastMessageAt,
    int UnreadCount);
