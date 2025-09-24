using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HousingHub.Model.Entities;

namespace HousingHub.Data.EntityConfigurations;

public class CustomerConfig : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.HasIndex(f => f.Email)
            .IsUnique(true)
            .HasDatabaseName("IX_Customers_Email");

        builder.HasIndex(f => f.PhoneNumber)
            .IsUnique(true)
            .HasDatabaseName("IX_Customers_PhoneNumber");

        builder.HasIndex(f => f.NationalIdNumber)
            .IsUnique(true)
            .HasDatabaseName("IX_Customers_NationalIdNumber");

        builder.HasIndex(f => f.CustomerType)
            .HasDatabaseName("IX_Customers_CustomerType");

        builder.HasIndex(f => f.DateCreated)
            .HasDatabaseName("IX_Customers_DateCreated");

        builder.HasIndex(f => f.IsKycVerified)
            .HasDatabaseName("IX_Customers_IsKycVerified");

        // Relationships
        builder.HasOne(c => c.Address)
            .WithOne()
            .HasForeignKey<Customer>(c => c.AddressId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
