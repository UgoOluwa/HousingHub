using Microsoft.EntityFrameworkCore;
using HousingHub.Data.EntityConfigurations;
using HousingHub.Model.Entities;

namespace HousingHub.Data.Contexts;

public class AppDbContext : DbContext
{
    public AppDbContext()
    {
        
    }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Server=localhost;Database=housing;Username=postgres;Password=admin;");
    }

    public DbSet<PropertyFile> PropertyFiles { get; set; } = default!;
    public DbSet<Property> Properties { get; set; } = default!;
    public DbSet<PropertyAddress> PropertyAddresses { get; set; } = default!;
    public DbSet<PropertyInterest> PropertyInterests { get; set; } = default!;

    public DbSet<Customer> Customers { get; set; } = default!;
    public DbSet<CustomerAddress> CustomerAddresses { get; set; } = default!;


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        #region Fluent Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerConfig).Assembly);
        #endregion

    }
}
