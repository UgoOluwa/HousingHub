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

    // ── Did it actually happen? ─────────────────────────────────
    // A booking is not an attendance. Everything downstream — choosing a tenant
    // above all — rests on somebody saying the viewing took place, and the renter
    // is the only party who both booked it and knows.

    /// <summary>When the renter confirmed the viewing happened.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Whatever the renter wanted to say about the viewing. Optional.
    /// </summary>
    /// <remarks>
    /// Shown to the owner, because it is the only account of the viewing anybody
    /// writes down — "the gate was locked and I saw it from outside" changes what
    /// "completed" means here, and an owner about to choose a tenant should read it.
    /// </remarks>
    public string? CompletionComment { get; set; }

    /// <summary>Set when the "did this happen?" email went out, so it is only ever asked once.</summary>
    public DateTime? CompletionFollowUpSentAt { get; set; }

    /// <summary>
    /// The slot, as a single instant.
    /// </summary>
    /// <remarks>
    /// Accepting a reschedule copies the new date and time onto ScheduledDate and
    /// ScheduledTime, so this is always the current slot rather than the original.
    /// <para>
    /// The stored time carries no zone and is compared against UTC throughout —
    /// the reminder worker does the same. Lagos is UTC+1 with no daylight saving, so
    /// a slot typed as 10:00 is treated as an hour earlier than the renter meant.
    /// Harmless for reminders and for this, both of which have hours of slack, but it
    /// is a real inconsistency and not one to build anything precise on.
    /// </para>
    /// </remarks>
    [DynamoDBIgnore]
    public DateTime ScheduledAt => ScheduledDate.Date + ScheduledTime;

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

    /// <summary>
    /// The renter says the viewing happened.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Confirmed only. A pending one was never agreed, a declined or cancelled one
    /// was called off, and an expired one is closed — none of those is a viewing
    /// somebody attended.
    /// </para>
    /// <para>
    /// Allowed from the scheduled day onward rather than from the exact minute.
    /// Owners let people in early, the stored time has no zone (see
    /// <see cref="ScheduledAt"/>), and refusing a renter who is telling the truth is
    /// worse than the thing the check is for — which is stopping somebody booking a
    /// slot next month and marking it done today to qualify as a tenant.
    /// </para>
    /// </remarks>
    public bool TryComplete(string? comment, DateTime? asOfUtc = null)
    {
        if (Status != InspectionStatus.Confirmed) return false;

        var now = asOfUtc ?? DateTime.UtcNow;
        if (ScheduledDate.Date > now.Date) return false;

        Status = InspectionStatus.Completed;
        CompletedAt = now;
        CompletionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        DateModified = now;
        return true;
    }

    /// <summary>
    /// Nobody ever said whether it happened, so it is closed.
    /// </summary>
    /// <remarks>
    /// Refused unless the renter was actually asked. Expiring an inspection we never
    /// chased would punish somebody for our own silence, and the whole point of the
    /// state is that a question went unanswered.
    /// </remarks>
    public bool TryExpire()
    {
        if (Status != InspectionStatus.Confirmed) return false;
        if (CompletionFollowUpSentAt is null) return false;

        Status = InspectionStatus.Expired;
        DateModified = DateTime.UtcNow;
        return true;
    }

    /// <summary>Records that we have asked. Returns false if we already had.</summary>
    public bool TryMarkFollowUpSent(DateTime? asOfUtc = null)
    {
        if (Status != InspectionStatus.Confirmed) return false;
        if (CompletionFollowUpSentAt is not null) return false;

        CompletionFollowUpSentAt = asOfUtc ?? DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        return true;
    }

    private static string GenerateInspectionId()
    {
        return $"INS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
    }
}
