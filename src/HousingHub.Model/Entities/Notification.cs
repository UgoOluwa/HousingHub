using System.ComponentModel.DataAnnotations;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

public class Notification : BaseEntity
{
    public Guid RecipientId { get; set; }
    public Customer Recipient { get; set; } = null!;

    public Guid? InspectionId { get; set; }
    public PropertyInspection? Inspection { get; set; }

    public NotificationType Type { get; set; }

    [StringLength(500)]
    public string Title { get; set; } = null!;

    [StringLength(2000)]
    public string Message { get; set; } = null!;

    public bool IsRead { get; set; } = false;

    public Notification() { }

    public Notification(Guid recipientId, Guid? inspectionId, NotificationType type, string title, string message)
    {
        Id = Guid.NewGuid();
        RecipientId = recipientId;
        InspectionId = inspectionId;
        Type = type;
        Title = title;
        Message = message;
    }
}
