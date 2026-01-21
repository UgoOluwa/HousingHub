namespace HousingHub.Model.Entities;

public class PropertyInterest : BaseEntity
{
    // Relationships
    public Guid CustomerId { get; set; } // FK
    public Customer Customer { get; set; } = null!;

    public Guid PropertyId { get; set; } // FK
    public Property Property { get; set; } = null!;

    public PropertyInterest() { }
    public PropertyInterest(Guid customerId, Guid propertyId)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        PropertyId = propertyId;
    }
}
