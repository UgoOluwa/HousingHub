using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

[DynamoDBTable("Properties")]
public class Property : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("PropertyId-index")]
    public string PropertyId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public PropertyType PropertyType { get; set; }
    public decimal Price { get; set; }
    public PropertyAvailability Availability { get; set; } = PropertyAvailability.Available;
    public PropertyLeaseType PropertyLeaseType { get; set; }
    public PropertyFeature Features { get; set; } = PropertyFeature.None;

    /// <summary>Number of bedrooms, or null when the lister did not state one.</summary>
    /// <remarks>
    /// <para>
    /// Nullable rather than defaulting to 0, because 0 and "not stated" are different
    /// answers and a renter reads them differently. Land genuinely has no bedrooms;
    /// a listing created before this field existed simply never said. Defaulting to 0
    /// would render both as "0 Bedrooms" and make the second one a false statement.
    /// </para>
    /// <para>
    /// Every row written before this existed reads as null, and the bedroom filter
    /// excludes them — a listing cannot match "3 bedrooms" when nobody said how many
    /// it has. Owners fill this in by editing the listing.
    /// </para>
    /// </remarks>
    public int? Bedrooms { get; set; }

    /// <summary>Number of bathrooms, or null when the lister did not state one.</summary>
    /// <remarks>Nullable for the same reason as <see cref="Bedrooms"/>.</remarks>
    public int? Bathrooms { get; set; }

    // Contact person
    public string? ContactPersonName { get; set; }
    public string? ContactPersonEmail { get; set; }
    public string? ContactPersonPhoneNumber { get; set; }

    // Relationships (foreign keys only, navigation properties ignored by DynamoDB)
    [DynamoDBGlobalSecondaryIndexHashKey("OwnerId-index")]
    public Guid OwnerId { get; set; }
    [DynamoDBIgnore]
    public Customer Owner { get; set; } = null!;
    [DynamoDBIgnore]
    public ICollection<PropertyFile> Files { get; set; } = new List<PropertyFile>();
    [DynamoDBIgnore]
    public ICollection<PropertyInspection> Inspections { get; set; } = new List<PropertyInspection>();
    [DynamoDBIgnore]
    public PropertyAddress Address { get; set; } = null!;
    public Guid AddressId { get; set; }

    // Geolocation
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Analytics
    public long ViewCount { get; set; }

    // Admin moderation
    public bool IsPublished { get; set; } = false;

    /// <summary>Attribute value written for a published listing. Absent otherwise.</summary>
    public const string PublishedMarker = "PUBLISHED";

    /// <summary>
    /// Index key mirroring <see cref="IsPublished"/>, so "every published listing" is a
    /// Query rather than a scan of the whole table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bool cannot be a DynamoDB key, and even if it could, an index on one would put
    /// every published row under a single partition key. This projects the flag to a
    /// string that is <i>absent</i> when false — DynamoDB global secondary indexes are
    /// sparse, so unpublished listings never enter the index at all and a query against
    /// it reads only rows it is going to return.
    /// </para>
    /// <para>
    /// <b>The setter deliberately discards its argument.</b> The value is derived from
    /// <see cref="IsPublished"/>, which is the single source of truth; a setter exists
    /// only because the DynamoDB object mapper requires one to deserialize. Storing what
    /// comes back from the table would make the two fields capable of disagreeing, and
    /// worse, would depend on the order the mapper happens to assign properties in —
    /// which is not specified. Ignoring it means the pair cannot drift.
    /// </para>
    /// <para>
    /// Rows written before this existed have no marker and are therefore invisible to
    /// the index until they are re-saved. See docs/data-backfill-required.md; the read
    /// path stays on the old scan until <c>Dynamo:UsePublishedIndex</c> is switched on.
    /// </para>
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("PublishedStatus-index")]
    public string? PublishedStatus
    {
        get => IsPublished ? PublishedMarker : null;
        set { /* derived from IsPublished — see remarks */ }
    }

    public DateTime? PublishedAt { get; set; }
    /// <summary>
    /// Admin moderation flag — "we have looked at this listing".
    /// </summary>
    /// <remarks>
    /// Deliberately NOT the same thing as title verification, and must not be
    /// rendered as if it were. This says an admin reviewed the listing; it says
    /// nothing about who owns the property. See
    /// <see cref="TitleVerificationTier"/> for the claim that does.
    /// </remarks>
    public bool IsVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; }

    // ── Title verification ──────────────────────────────────────
    // Set when a VerificationCase of type Property is approved. Written only by
    // VerificationService.

    /// <summary>
    /// How far this property's title has been verified.
    /// </summary>
    /// <remarks>
    /// The strongest claim the platform can make, and the most dangerous to make
    /// loosely — a defrauded buyer will point at whatever badge this drives.
    /// </remarks>
    public VerificationTier TitleVerificationTier { get; set; } = VerificationTier.Unverified;

    public DateTime? TitleVerifiedAt { get; set; }

    /// <summary>
    /// Name on the title document, as recorded by the reviewer.
    /// </summary>
    /// <remarks>
    /// Kept because the comparison between this and the lister is the check that
    /// catches the most common fraud in this market: a real Certificate of
    /// Occupancy belonging to somebody else.
    /// </remarks>
    public string? TitleHolderName { get; set; }

    /// <summary>
    /// False when the person listing the property is not the title holder.
    /// </summary>
    /// <remarks>
    /// A legitimate and common case — an agent acting for an owner, or a tenant
    /// subletting — but it changes what is required. When false, a Letter of
    /// Authority to Let is needed, and the badge must not imply the lister owns
    /// the property.
    /// </remarks>
    public bool ListerIsTitleHolder { get; set; } = true;

    /// <summary>True when title verification is current.</summary>
    [DynamoDBIgnore]
    public bool IsTitleVerified => TitleVerificationTier >= VerificationTier.TitleVerified;
    /// <summary>Reason an admin gave when unpublishing this listing; cleared on republish.</summary>
    public string? UnpublishReason { get; set; }

    /// <summary>Set when this property was created despite a possible-duplicate warning being overridden, so admins can review it later.</summary>
    public bool IsFlaggedDuplicate { get; set; } = false;
    /// <summary>The existing property this one was flagged as a possible duplicate of. Null unless <see cref="IsFlaggedDuplicate"/> is set.</summary>
    public Guid? PossibleDuplicateOfPropertyId { get; set; }

    public Property() { }

    public Property(string title, string description, PropertyType propertyType, decimal price, PropertyAvailability availability, PropertyLeaseType propertyLeaseType)
    {
        Id = Guid.NewGuid();
        PropertyId = GeneratePropertyId();
        Title = title;
        Description = description;
        PropertyType = propertyType;
        Price = price;
        Availability = availability;
        PropertyLeaseType = propertyLeaseType;
    }

    private static string GeneratePropertyId()
    {
        return $"PROP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
    }
}
