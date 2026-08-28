using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

[DynamoDBTable("PropertyInspections")]
public class PropertyInspection : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("InspectionId-index")]
    public string InspectionId { get; set; } = null!;

    [DynamoDBGlobalSecondaryIndexHashKey("CustomerId-index")]
    public Guid CustomerId { get; set; }
    [DynamoDBIgnore]
    public Customer Customer { get; set; } = null!;

    [DynamoDBGlobalSecondaryIndexHashKey("PropertyId-index")]
    public Guid PropertyId { get; set; }
    [DynamoDBIgnore]
    public Property Property { get; set; } = null!;

    public DateTime ScheduledDate { get; set; }
    public long ScheduledTimeTicks { get; set; }

    public string? Note { get; set; }

    public InspectionStatus Status { get; set; } = InspectionStatus.Pending;

    public string? DeclineNote { get; set; }

    public DateTime? RescheduledDate { get; set; }
    public long? RescheduledTimeTicks { get; set; }

    public string? RescheduleNote { get; set; }

    /// <summary>
    /// Who proposed the pending reschedule. Null on inspections that have never been
    /// rescheduled, and on rows written before this existed.
    /// </summary>
    /// <remarks>
    /// Without it, "respond to a reschedule" cannot tell the two parties apart: both
    /// were shown Accept/Decline for a date one of them had just proposed, and the
    /// server let the proposer accept their own suggestion.
    /// </remarks>
    public Guid? RescheduleRequestedById { get; set; }

    /// <summary>Set once the 24-hour reminder has been sent, so the reminder worker never sends it twice.</summary>
    public DateTime? ReminderSentAt { get; set; }

    /// <summary>Set when the property owner hands this inspection off to HousingHub staff instead of managing it themselves. Null means it's still owner-managed.</summary>
    public DateTime? HandedOffAt { get; set; }

    /// <summary>The staff Admin a SuperAdmin assigned this inspection to, after it was handed off. Null means handed off but not yet assigned.</summary>
    public Guid? AssignedStaffId { get; set; }

    [DynamoDBIgnore]
    public TimeSpan ScheduledTime
    {
        get => TimeSpan.FromTicks(ScheduledTimeTicks);
        set => ScheduledTimeTicks = value.Ticks;
    }

    [DynamoDBIgnore]
    public TimeSpan? RescheduledTime
    {
        get => RescheduledTimeTicks.HasValue ? TimeSpan.FromTicks(RescheduledTimeTicks.Value) : null;
        set => RescheduledTimeTicks = value?.Ticks;
    }

    public PropertyInspection() { }

    public PropertyInspection(Guid customerId, Guid propertyId, DateTime scheduledDate, TimeSpan scheduledTime, string? note)
    {
        Id = Guid.NewGuid();
        InspectionId = GenerateInspectionId();
        CustomerId = customerId;
        PropertyId = propertyId;
        ScheduledDate = scheduledDate;
        ScheduledTime = scheduledTime;
        Note = note;
        Status = InspectionStatus.Pending;
    }

    private static string GenerateInspectionId()
    {
        return $"INS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
    }
}
