using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using HousingHub.Service.WeatherForcastService;
using HousingHub.Service.WeatherForcastService.Interfaces;

namespace HousingHub.Service;

public static class ConfigureServices
{
    public static IServiceCollection AddInjectionService(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddScoped<IWeatherForcastQueryService, WeatherForcastQueryService>();
        services.AddScoped<IWeatherForcastCommandService, WeatherForcastCommandService>();

        return services;
    }
}
