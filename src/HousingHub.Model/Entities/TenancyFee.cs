using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

/// <summary>
/// A cost on top of the rent, named by the owner.
/// </summary>
/// <remarks>
/// <para>
/// Legal fees, agency fees, cleaning, service charge — whatever the owner is
/// charging. Free-named for the same reason the documents are: the platform cannot
/// anticipate the list, and a fixed set would push real costs into a vague "other".
/// </para>
/// <para>
/// These are shown to the tenant <b>before</b> they sign or upload anything. A fee
/// that first appears at the payment step is a surprise at the worst possible
/// moment, and it is the point in the flow where deals die.
/// </para>
/// </remarks>
[DynamoDBTable("TenancyFees")]
public class TenancyFee : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("TenancyId-index")]
    public Guid TenancyId { get; set; }

    /// <summary>What the owner called it. Shown to the tenant verbatim.</summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// What it covers, in the owner's words.
    /// </summary>
    /// <remarks>
    /// Required rather than optional. A line reading "Agency fee — ₦250,000" with no
    /// explanation is the kind of charge a tenant disputes later, and the owner is
    /// the only one who can say what it is for.
    /// </remarks>
    public string Description { get; set; } = null!;

    /// <summary>Kobo, matching the rent and the payment rail.</summary>
    public long AmountKobo { get; set; }

    public TenancyFee() { }

    public TenancyFee(Guid tenancyId, string name, string description, long amountKobo)
    {
        Id = Guid.NewGuid();
        TenancyId = tenancyId;
        Name = name;
        Description = description;
        AmountKobo = amountKobo;
        IsActive = true;
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
    }
}
