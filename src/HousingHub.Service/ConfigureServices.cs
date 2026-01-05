using System.Reflection;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.CustomerAddressService;
using HousingHub.Service.CustomerAddressService.Interfaces;
using HousingHub.Service.CustomerService;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.PropertyAddressService;
using HousingHub.Service.PropertyAddressService.Interfaces;
using HousingHub.Service.PropertyFileService;
using HousingHub.Service.PropertyFileService.Interfaces;
using HousingHub.Service.PropertyInterestService;
using HousingHub.Service.PropertyInterestService.Interfaces;
using HousingHub.Service.PropertyService;
using HousingHub.Service.PropertyService.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HousingHub.Service;

public static class ConfigureServices
{
    public static IServiceCollection AddInjectionService(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => {
            cfg.AddProfile<CustomerAddressMapper>();
            cfg.AddProfile<CustomerMapper>();
            cfg.AddProfile<PropertyAddressMapper>();
            cfg.AddProfile<PropertyFileMapper>();
            cfg.AddProfile<PropertyInterestMapper>();
            cfg.AddProfile<PropertyMapper>();
        });
        services.AddScoped<ICustomerCommandService, CustomerCommandService>();
        services.AddScoped<ICustomerQueryService, CustomerQueryService>();
        services.AddScoped<IPropertyCommandService, PropertyCommandService>();
        services.AddScoped<IPropertyQueryService, PropertyQueryService>();
        services.AddScoped<ICustomerAddressCommandService, CustomerAddressCommandService>();
        services.AddScoped<ICustomerAddressQueryService, CustomerAddressQueryService>();
        services.AddScoped<IPropertyAddressQueryService, PropertyAddressQueryService>();
        services.AddScoped<IPropertyAddressCommandService, PropertyAddressCommandService>();
        services.AddScoped<IPropertyInterestCommandService, PropertyInterestCommandService>();
        services.AddScoped<IPropertyInterestQueryService, PropertyInterestQueryService>();
        services.AddScoped<IPropertyFileCommandService, PropertyFileCommandService>();
        services.AddScoped<IPropertyFileQueryService, PropertyFileQueryService>();


        return services;
    }
}
