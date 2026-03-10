using System.ComponentModel.DataAnnotations;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

public class PropertyInspection : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public DateTime ScheduledDate { get; set; }
    public TimeSpan ScheduledTime { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    public InspectionStatus Status { get; set; } = InspectionStatus.Pending;

    [StringLength(1000)]
    public string? DeclineNote { get; set; }

    public DateTime? RescheduledDate { get; set; }
    public TimeSpan? RescheduledTime { get; set; }

    [StringLength(1000)]
    public string? RescheduleNote { get; set; }

    public PropertyInspection() { }

    public PropertyInspection(Guid customerId, Guid propertyId, DateTime scheduledDate, TimeSpan scheduledTime, string? note)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        PropertyId = propertyId;
        ScheduledDate = scheduledDate;
        ScheduledTime = scheduledTime;
        Note = note;
        Status = InspectionStatus.Pending;
    }
}
