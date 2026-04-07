using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

[DynamoDBTable("PropertyAddresses")]
public class PropertyAddress : BaseEntity
{
    public string Place { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string PostalCode { get; set; } = null!;

    // Relationship
    [DynamoDBIgnore]
    public Property Property { get; set; } = null!;
    [DynamoDBGlobalSecondaryIndexHashKey("PropertyId-index")]
    public Guid PropertyId { get; set; }

    public PropertyAddress(){ }

    public PropertyAddress(string place, string city, string state, string country, string postalCode)
    {
        Id = Guid.NewGuid();
        Place = place;
        City = city;
        State = state;
        Country = country;
        PostalCode = postalCode;
    }
}
