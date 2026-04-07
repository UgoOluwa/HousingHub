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
