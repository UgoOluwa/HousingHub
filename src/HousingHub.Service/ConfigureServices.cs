using System.Reflection;
using Amazon.S3;
using HousingHub.Service.AuthService;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.CustomerAddressService;
using HousingHub.Service.CustomerAddressService.Interfaces;
using HousingHub.Service.CustomerService;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.InspectionService;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.NotificationService;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.PropertyAddressService;
using HousingHub.Service.PropertyAddressService.Interfaces;
using HousingHub.Service.PropertyFileService;
using HousingHub.Service.PropertyFileService.Interfaces;
using HousingHub.Service.PropertyService;
using HousingHub.Service.PropertyService.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SendGrid;

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
            cfg.AddProfile<InspectionMapper>();
            cfg.AddProfile<PropertyMapper>();
        });

        // Auth
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();
        services.AddScoped<IAuthService, AuthService.AuthService>();

        // Email (SendGrid)
        services.AddSingleton<ISendGridClient>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            string apiKey = configuration["Email:SendGridApiKey"]!;
            return new SendGridClient(apiKey);
        });
        services.AddScoped<IEmailService, SendGridEmailService>();

        services.AddScoped<ICustomerCommandService, CustomerCommandService>();
        services.AddScoped<ICustomerQueryService, CustomerQueryService>();
        services.AddScoped<IPropertyCommandService, PropertyCommandService>();
        services.AddScoped<IPropertyQueryService, PropertyQueryService>();
        services.AddScoped<ICustomerAddressCommandService, CustomerAddressCommandService>();
        services.AddScoped<ICustomerAddressQueryService, CustomerAddressQueryService>();
        services.AddScoped<IPropertyAddressQueryService, PropertyAddressQueryService>();
        services.AddScoped<IPropertyAddressCommandService, PropertyAddressCommandService>();
        services.AddScoped<IInspectionCommandService, InspectionCommandService>();
        services.AddScoped<IInspectionQueryService, InspectionQueryService>();
        services.AddScoped<INotificationCommandService, NotificationCommandService>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddScoped<IPropertyFileCommandService, PropertyFileCommandService>();
        services.AddScoped<IPropertyFileQueryService, PropertyFileQueryService>();

        // AWS S3 File Storage
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(configuration["AWS:S3:Region"]!)
            };
            return new AmazonS3Client(
                configuration["AWS:S3:AccessKey"],
                configuration["AWS:S3:SecretKey"],
                config);
        });
        services.AddSingleton<IFileStorageService, S3FileStorageService>();


        return services;
    }
}
