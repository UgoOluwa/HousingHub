using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HousingHub.Model.Entities;

namespace HousingHub.Data.EntityConfigurations;

public class PropertyFileConfig : IEntityTypeConfiguration<PropertyFile>
{
    public void Configure(EntityTypeBuilder<PropertyFile> builder)
    {
        builder.ToTable("PropertyFiles");
        builder.HasKey(pf => pf.Id);

        // Relationships
        builder.HasOne(ca => ca.Property)
            .WithMany(c => c.Files)
            .HasForeignKey(ca => ca.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
