using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

[DynamoDBTable("CustomerAddresses")]
public class CustomerAddress : BaseEntity
{
    public string Street { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string PostalCode { get; set; } = null!;

    // Relationship
    [DynamoDBIgnore]
    public Customer Customer { get; set; } = null!;
    [DynamoDBGlobalSecondaryIndexHashKey("CustomerId-index")]
    public Guid CustomerId { get; set; }

    public CustomerAddress(){}

    public CustomerAddress(string street, string city, string state, string country, string postalCode, Guid customerId)
    {
        Id = Guid.NewGuid();
        Street = street;
        City = city;
        State = state;
        Country = country;
        PostalCode = postalCode;
        CustomerId = customerId;
    }
}
