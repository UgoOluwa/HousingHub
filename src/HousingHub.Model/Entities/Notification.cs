using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

[DynamoDBTable("Notifications")]
public class Notification : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("RecipientId-index")]
    public Guid RecipientId { get; set; }
    [DynamoDBIgnore]
    public Customer Recipient { get; set; } = null!;

    public Guid? InspectionId { get; set; }
    [DynamoDBIgnore]
    public PropertyInspection? Inspection { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = null!;

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
