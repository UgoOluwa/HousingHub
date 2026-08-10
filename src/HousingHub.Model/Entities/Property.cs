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
    public bool IsVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; }
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
