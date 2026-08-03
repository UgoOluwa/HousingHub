using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

/// <summary>
/// A customer's saved search — when a property is published (see
/// <see cref="PropertyService.PropertyCommandService"/>) matching all set fields,
/// the customer gets notified. Null fields on a preference mean "any" for that
/// dimension. Reuses <see cref="BaseEntity.IsActive"/> as the enabled/disabled flag.
/// </summary>
[DynamoDBTable("PropertyAlertPreferences")]
public class PropertyAlertPreference : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("CustomerId-index")]
    public Guid CustomerId { get; set; }
    [DynamoDBIgnore]
    public Customer Customer { get; set; } = null!;

    public PropertyType? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public PropertyFeature? Features { get; set; }

    public PropertyAlertPreference() { }

    public PropertyAlertPreference(Guid customerId, PropertyType? propertyType, decimal? minPrice, decimal? maxPrice, string? city, string? state, PropertyFeature? features)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        PropertyType = propertyType;
        MinPrice = minPrice;
        MaxPrice = maxPrice;
        City = city;
        State = state;
        Features = features;
        IsActive = true;
    }

    /// <summary>True if the given property (with its resolved city/state) satisfies every set dimension of this preference.</summary>
    public bool Matches(Property property, string? propertyCity, string? propertyState)
    {
        if (PropertyType.HasValue && PropertyType.Value != property.PropertyType) return false;
        if (MinPrice.HasValue && property.Price < MinPrice.Value) return false;
        if (MaxPrice.HasValue && property.Price > MaxPrice.Value) return false;
        if (!string.IsNullOrWhiteSpace(City) && !string.Equals(City, propertyCity, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(State) && !string.Equals(State, propertyState, StringComparison.OrdinalIgnoreCase)) return false;
        if (Features.HasValue && Features.Value != PropertyFeature.None && (property.Features & Features.Value) != Features.Value) return false;
        return true;
    }
}
