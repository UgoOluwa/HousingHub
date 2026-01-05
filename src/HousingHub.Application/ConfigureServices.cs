using System.Reflection;
using FluentValidation;
using HousingHub.Application.Commons.Behaviours;
using HousingHub.Application.Commons.Mappings;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HousingHub.Application;

public static class ConfigureServices
{
    public static void AddInjectionApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies()));
        services.AddAutoMapper(cfg => {
            cfg.AddProfile<MappingProfile>();
        });
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
    }
}
