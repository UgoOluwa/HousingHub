using System.ComponentModel.DataAnnotations;

namespace HousingHub.Model.Entities;

public class CustomerAddress : BaseEntity
{
    [StringLength(1000)]
    public string Street { get; set; } = null!;
    [StringLength(100)]
    public string City { get; set; } = null!;
    [StringLength(100)]
    public string State { get; set; } = null!;
    [StringLength(100)]
    public string Country { get; set; } = null!;
    [StringLength(20)]
    public string PostalCode { get; set; } = null!;

    // Relationship
    public Customer Customer { get; set; } = null!;
    public Guid CustomerId { get; set; }

    public CustomerAddress(){}

    public CustomerAddress(string street, string city, string state, string country, string postalCode)
    {
        Id = Guid.NewGuid();
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        Street = street;
        City = city;
        State = state;
        Country = country;
        PostalCode = postalCode;
    }
}
