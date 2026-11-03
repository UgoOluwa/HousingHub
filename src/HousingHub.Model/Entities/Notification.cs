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

    /// <summary>Set for property-alert-match notifications, so the FE can deep-link to the listing.</summary>
    public Guid? PropertyId { get; set; }
    [DynamoDBIgnore]
    public Property? Property { get; set; }

    /// <summary>
    /// Set for tenancy notifications, so the FE can deep-link to the tenancy.
    /// </summary>
    /// <remarks>
    /// Its own field rather than reusing <see cref="InspectionId"/>. A tenancy id
    /// sitting in a field called InspectionId reads as an inspection to everything
    /// downstream, and the first client to follow that link would send somebody to a
    /// viewing that does not exist.
    /// </remarks>
    public Guid? TenancyId { get; set; }

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

    /// <summary>
    /// A notification about a tenancy.
    /// </summary>
    /// <remarks>
    /// A factory rather than a constructor: the property-id overload already takes
    /// (Guid, NotificationType, string, string, Guid?), so a tenancy overload would
    /// be chosen by argument order alone — which is how a tenancy id ends up filed
    /// as a property id with nothing failing to say so.
    /// </remarks>
    public static Notification ForTenancy(
        Guid recipientId, Guid tenancyId, NotificationType type, string title, string message) =>
        new()
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            TenancyId = tenancyId,
            Type = type,
            Title = title,
            Message = message,
        };

    public Notification(Guid recipientId, NotificationType type, string title, string message, Guid? propertyId)
    {
        Id = Guid.NewGuid();
        RecipientId = recipientId;
        PropertyId = propertyId;
        Type = type;
        Title = title;
        Message = message;
    }
}
