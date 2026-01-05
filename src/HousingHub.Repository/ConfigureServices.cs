using Microsoft.Extensions.DependencyInjection;
using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Repository.Commands;
using HousingHub.Repository.Queries;

namespace HousingHub.Repository;

public static class ConfigureServices
{
    public static IServiceCollection AddInjectionRepository(this IServiceCollection services)
    {
        services.AddScoped<AppDbContext>();
        services.AddScoped<IUnitOfWOrk, UnitOfWork>();
        services.AddScoped(typeof(IGenericCommandRepository<>), typeof(GenericCommandRepository<>));
        services.AddScoped(typeof(IGenericQueryRepository<>), typeof(GenericQueryRepository<>));
        services.AddScoped<IPropertyQueryRepository, PropertyQueryRepository>();
        services.AddScoped<IPropertyAddressQueryRepository, PropertyAddressQueryRepository>();
        services.AddScoped<IPropertyFileQueryRepository, PropertyFileQueryRepository>();
        services.AddScoped<IPropertyInterestQueryRepository, PropertyInterestQueryRepository>();
        services.AddScoped<IPropertyCommandRepository, PropertyCommandRepository>();
        services.AddScoped<IPropertyAddressCommandRepository, PropertyAddressCommandRepository>();
        services.AddScoped<IPropertyFileCommandRepository, PropertyFileCommandRepository>();
        services.AddScoped<IPropertyInterestCommandRepository, PropertyInterestCommandRepository>();
        services.AddScoped<ICustomerAddressCommandRepository, CustomerAddressCommandRepository>();
        services.AddScoped<ICustomerCommandRepository, CustomerCommandRepository>();
        services.AddScoped<ICustomerQueryRepository, CustomerQueryRepository>();
        services.AddScoped<ICustomerAddressQueryRepository, CustomerAddressQueryRepository>();

        return services;
    }
}
