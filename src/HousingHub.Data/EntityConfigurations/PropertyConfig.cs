using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HousingHub.Model.Entities;

namespace HousingHub.Data.EntityConfigurations;

public class PropertyConfig : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.PropertyType)
            .HasDatabaseName("IX_Properties_PropertyType");

        builder.HasIndex(p => p.Price)
            .HasDatabaseName("IX_Properties_Price");

        builder.HasIndex(p => p.IsAvailable)
            .HasDatabaseName("IX_Properties_IsAvailable");

        builder.HasIndex(p => p.PropertyLeaseType)
            .HasDatabaseName("IX_Properties_PropertyLeaseType");

        builder.HasIndex(p => p.OwnerId)
            .HasDatabaseName("IX_Properties_OwnerId");

        // Relationships
        builder.HasOne(p => p.Owner)
            .WithMany(c => c.Properties)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Address)
            .WithOne()
            .HasForeignKey<Property>(p => p.AddressId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PropertyInterestConfig : IEntityTypeConfiguration<PropertyInterest>
{
    public void Configure(EntityTypeBuilder<PropertyInterest> builder)
    {
        builder.ToTable("PropertyInterests");
        builder.HasKey(pi => pi.Id);

        builder.HasIndex(pi => pi.CustomerId)
            .HasDatabaseName("IX_PropertyInterests_CustomerId");

        builder.HasIndex(pi => pi.PropertyId)
            .HasDatabaseName("IX_PropertyInterests_PropertyId");

        // Relationships
        builder.HasOne(pi => pi.Customer)
            .WithMany(c => c.Interests)
            .HasForeignKey(pi => pi.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pi => pi.Property)
            .WithMany(p => p.Interests)
            .HasForeignKey(pi => pi.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
