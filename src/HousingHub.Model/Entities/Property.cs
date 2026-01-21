using System.ComponentModel.DataAnnotations;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

public class Property : BaseEntity
{
    [StringLength(500)]
    public string Title { get; set; } = null!;
    [StringLength(1000)]
    public string Description { get; set; } = null!;
    public PropertyType PropertyType { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
    public PropertyLeaseType PropertyLeaseType { get; set; }

    // Relationships
    public Guid OwnerId { get; set; } // FK to Customer
    public Customer Owner { get; set; } = null!;
    public ICollection<PropertyFile> Files { get; set; } = new List<PropertyFile>();
    public ICollection<PropertyInterest> Interests { get; set; } = new List<PropertyInterest>();
    public PropertyAddress Address { get; set; } = null!;
    public Guid AddressId { get; set; }


    public Property(){}

    public Property(string title, string description, PropertyType propertyType, decimal price, bool isAvailable, PropertyLeaseType propertyLeaseType)
    {
        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        PropertyType = propertyType;
        Price = price;
        IsAvailable = isAvailable;
        PropertyLeaseType = propertyLeaseType;
    }
}
