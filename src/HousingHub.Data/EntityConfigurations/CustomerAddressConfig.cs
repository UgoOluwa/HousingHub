using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HousingHub.Model.Entities;

namespace HousingHub.Data.EntityConfigurations;

public class CustomerAddressConfig : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses");
        builder.HasKey(ca => ca.Id);

        builder.HasIndex(ca => ca.DateCreated)
            .HasDatabaseName("IX_CustomerAddresses_DateCreated");

        builder.HasIndex(ca => ca.State)
            .HasDatabaseName("IX_CustomerAddresses_State");

        builder.HasIndex(ca => ca.City)
            .HasDatabaseName("IX_CustomerAddresses_City");

        builder.HasIndex(ca => ca.Country)
            .HasDatabaseName("IX_CustomerAddresses_Country");
    }
}
