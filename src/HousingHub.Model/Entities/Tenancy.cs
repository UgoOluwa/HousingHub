using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

/// <summary>
/// One candidate, one property, from the moment the owner picks them.
/// </summary>
/// <remarks>
/// <para>
/// The spine of everything after an inspection. Documents, fees and the rent payment
/// all hang off this record, which is why it is created at selection rather than when
/// the first document is requested — there has to be something for them to attach to.
/// </para>
/// <para>
/// <b>At most one live tenancy per property.</b> Selecting a second candidate while
/// one is still in progress is refused rather than queued: two people cannot both be
/// taking the same flat, and a queue would mean telling the second one something that
/// is not true yet.
/// </para>
/// </remarks>
[DynamoDBTable("Tenancies")]
public class Tenancy : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("PropertyId-index")]
    public Guid PropertyId { get; set; }

    /// <summary>
    /// Who the tenant deals with, and who gets paid — captured at selection.
    /// </summary>
    /// <remarks>
    /// Whoever listed the property, which is the owner or the managing agent. There
    /// is deliberately only one counterparty: where an agent manages for a landlord,
    /// the platform interacts with the agent alone and the landlord is not a party
    /// here. Snapshotted rather than read through the property so a later change of
    /// lister cannot move the agreement underneath either side.
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("LandlordCustomerId-index")]
    public Guid LandlordCustomerId { get; set; }

    [DynamoDBGlobalSecondaryIndexHashKey("TenantCustomerId-index")]
    public Guid TenantCustomerId { get; set; }

    public TenancyStatus Status { get; set; } = TenancyStatus.CandidateSelected;

    /// <summary>
    /// The rent as it stood when the candidate was selected, in kobo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A snapshot, not a reference. The listing's price can be edited at any time and
    /// what was agreed must not move underneath either party — a tenant who was
    /// selected at one rent should never discover a different one at the payment step.
    /// </para>
    /// <para>
    /// Kobo as a whole number, matching the payment rail, so the figure that reaches
    /// Paystack is the figure recorded here with no conversion in between. The one
    /// conversion from the listing's naira price happens at selection and is rounded
    /// to the nearest kobo.
    /// </para>
    /// </remarks>
    public long AgreedRentKobo { get; set; }

    /// <summary>
    /// Rent, lease or sale, as the listing said at selection.
    /// </summary>
    /// <remarks>
    /// Snapshotted because it decides whether the agreement can be signed
    /// electronically at all — Nigeria's Evidence Act excludes land instruments, so a
    /// sale or a lease over three years has to be executed on paper. Reading it live
    /// would let an edit to the listing change the legal path mid-flow.
    /// </remarks>
    public PropertyLeaseType LeaseType { get; set; }

    /// <summary>The completed inspection this candidate was picked from.</summary>
    /// <remarks>
    /// Kept as provenance: an owner selects from people who actually viewed the
    /// property, and this records which viewing.
    /// </remarks>
    public Guid SelectedFromInspectionId { get; set; }

    public DateTime SelectedAt { get; set; }

    /// <summary>Why the owner pulled out. Shown to the candidate.</summary>
    public string? WithdrawnReason { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// True while this tenancy still occupies the property.
    /// </summary>
    /// <remarks>
    /// The test for "can another candidate be selected". Anything in the terminal
    /// band has released the property; everything else, including a completed and
    /// running let, still holds it.
    /// </remarks>
    [DynamoDBIgnore]
    public bool IsLive =>
        Status is not (TenancyStatus.Withdrawn or TenancyStatus.DeclinedByCandidate or TenancyStatus.Ended);

    public Tenancy() { }

    public Tenancy(
        Guid propertyId,
        Guid landlordCustomerId,
        Guid tenantCustomerId,
        Guid selectedFromInspectionId,
        long agreedRentKobo,
        PropertyLeaseType leaseType)
    {
        Id = Guid.NewGuid();
        PropertyId = propertyId;
        LandlordCustomerId = landlordCustomerId;
        TenantCustomerId = tenantCustomerId;
        SelectedFromInspectionId = selectedFromInspectionId;
        AgreedRentKobo = agreedRentKobo;
        LeaseType = leaseType;
        Status = TenancyStatus.CandidateSelected;
        SelectedAt = DateTime.UtcNow;
        IsActive = true;
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
    }

    /// <summary>
    /// The owner pulls out.
    /// </summary>
    /// <remarks>
    /// Only before the let is running. Ending a tenancy that is already
    /// <see cref="TenancyStatus.Active"/> is a different act with different
    /// consequences — money has moved and somebody is living there — and it belongs
    /// to the tenancy-management phase, not here.
    /// </remarks>
    public bool TryWithdraw(string? reason)
    {
        if (!IsLive || Status == TenancyStatus.Active) return false;

        Status = TenancyStatus.Withdrawn;
        WithdrawnReason = reason;
        ClosedAt = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// The candidate says they are no longer interested.
    /// </summary>
    /// <remarks>
    /// Allowed up to the point the let starts, for the same reason as
    /// <see cref="TryWithdraw"/>. Held apart from a withdrawal because the two mean
    /// opposite things to whoever is on the other end.
    /// </remarks>
    public bool TryDecline()
    {
        if (!IsLive || Status == TenancyStatus.Active) return false;

        Status = TenancyStatus.DeclinedByCandidate;
        ClosedAt = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        return true;
    }
}
