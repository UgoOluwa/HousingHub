using System.ComponentModel.DataAnnotations;

namespace HousingHub.Model.Entities;

public class PropertyAddress : BaseEntity
{
    [StringLength(1000)]
    public string Place { get; set; } = null!;
    [StringLength(100)]
    public string City { get; set; } = null!;
    [StringLength(100)]
    public string State { get; set; } = null!;
    [StringLength(100)]
    public string Country { get; set; } = null!;
    [StringLength(20)]
    public string PostalCode { get; set; } = null!;

    // Relationship
    public Property Property { get; set; } = null!;
    public Guid PropertyId { get; set; }

    public PropertyAddress(){ }

    public PropertyAddress(string place, string city, string state, string country, string postalCode)
    {
        Id = Guid.NewGuid();
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        Place = place;
        City = city;
        State = state;
        Country = country;
        PostalCode = postalCode;
    }
}
