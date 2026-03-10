using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Notification;

public record NotificationDto(
    Guid Id,
    DateTime DateCreated,
    Guid RecipientId,
    Guid? InspectionId,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead);
