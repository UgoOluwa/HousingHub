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
        services.AddScoped<IWeatherForcastCommadRepository, WeatherForcastCommandRepository>();
        services.AddScoped<IWeatherForcastQueryRepository, WeatherForcastQueryRepository>();
        services.AddScoped(typeof(IGenericCommandRepository<>), typeof(GenericCommandRepository<>));
        services.AddScoped(typeof(IGenericQueryRepository<>), typeof(GenericQueryRepository<>));

        return services;
    }
}
