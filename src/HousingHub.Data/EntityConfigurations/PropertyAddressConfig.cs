using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HousingHub.Model.Entities;

namespace HousingHub.Data.EntityConfigurations;

public class PropertyAddressConfig : IEntityTypeConfiguration<PropertyAddress>
{
    public void Configure(EntityTypeBuilder<PropertyAddress> builder)
    {
        builder.ToTable("PropertyAddresses");
        builder.HasKey(pa => pa.Id);

        builder.HasIndex(pa => pa.City)
            .HasDatabaseName("IX_PropertyAddresses_City");

        builder.HasIndex(pa => pa.State)
            .HasDatabaseName("IX_PropertyAddresses_State");

        builder.HasIndex(pa => pa.Country)
            .HasDatabaseName("IX_PropertyAddresses_ZipCode");
    }
}
