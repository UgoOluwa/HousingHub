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

        builder.HasIndex(p => p.PropertyId)
            .IsUnique(true)
            .HasDatabaseName("IX_Properties_PropertyId");

        builder.HasIndex(p => p.PropertyType)
            .HasDatabaseName("IX_Properties_PropertyType");

        builder.HasIndex(p => p.Price)
            .HasDatabaseName("IX_Properties_Price");

        builder.HasIndex(p => p.Availability)
            .HasDatabaseName("IX_Properties_Availability");

        builder.HasIndex(p => p.PropertyLeaseType)
            .HasDatabaseName("IX_Properties_PropertyLeaseType");

        builder.HasIndex(p => p.OwnerId)
            .HasDatabaseName("IX_Properties_OwnerId");

        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");

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

public class PropertyInspectionConfig : IEntityTypeConfiguration<PropertyInspection>
{
    public void Configure(EntityTypeBuilder<PropertyInspection> builder)
    {
        builder.ToTable("PropertyInspections");
        builder.HasKey(pi => pi.Id);

        builder.HasIndex(pi => pi.CustomerId)
            .HasDatabaseName("IX_PropertyInspections_CustomerId");

        builder.HasIndex(pi => pi.PropertyId)
            .HasDatabaseName("IX_PropertyInspections_PropertyId");

        builder.HasIndex(pi => pi.Status)
            .HasDatabaseName("IX_PropertyInspections_Status");

        // Relationships
        builder.HasOne(pi => pi.Customer)
            .WithMany(c => c.Inspections)
            .HasForeignKey(pi => pi.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pi => pi.Property)
            .WithMany(p => p.Inspections)
            .HasForeignKey(pi => pi.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.HasIndex(n => n.RecipientId)
            .HasDatabaseName("IX_Notifications_RecipientId");

        builder.HasIndex(n => n.IsRead)
            .HasDatabaseName("IX_Notifications_IsRead");

        builder.HasOne(n => n.Recipient)
            .WithMany(c => c.Notifications)
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Inspection)
            .WithMany()
            .HasForeignKey(n => n.InspectionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
