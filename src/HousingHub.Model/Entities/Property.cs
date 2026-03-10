using System.ComponentModel.DataAnnotations;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

public class Property : BaseEntity
{
    [StringLength(20)]
    public string PropertyId { get; set; } = null!;

    [StringLength(500)]
    public string Title { get; set; } = null!;

    [StringLength(1000)]
    public string Description { get; set; } = null!;

    public PropertyType PropertyType { get; set; }
    public decimal Price { get; set; }
    public PropertyAvailability Availability { get; set; } = PropertyAvailability.Available;
    public PropertyLeaseType PropertyLeaseType { get; set; }
    public PropertyFeature Features { get; set; } = PropertyFeature.None;

    // Contact person
    [StringLength(200)]
    public string? ContactPersonName { get; set; }

    [StringLength(200)]
    public string? ContactPersonEmail { get; set; }

    [StringLength(50)]
    public string? ContactPersonPhoneNumber { get; set; }

    // Relationships
    public Guid OwnerId { get; set; }
    public Customer Owner { get; set; } = null!;
    public ICollection<PropertyFile> Files { get; set; } = new List<PropertyFile>();
    public ICollection<PropertyInspection> Inspections { get; set; } = new List<PropertyInspection>();
    public PropertyAddress Address { get; set; } = null!;
    public Guid AddressId { get; set; }

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
