using System.Reflection;
using Amazon.S3;
using HousingHub.Service.AdminService;
using HousingHub.Service.AuthService;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.ChatService;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Commons.Geocoding;
using HousingHub.Service.Commons.Utilities;
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
using HousingHub.Service.PropertyReportService;
using HousingHub.Service.PropertyReportService.Interfaces;
using HousingHub.Service.PropertyService;
using HousingHub.Service.PropertyService.Interfaces;
using HousingHub.Service.Commons.Mappings;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace HousingHub.Service;

public static class ConfigureServices
{
    public static IServiceCollection AddInjectionService(this IServiceCollection services)
    {
        var mapsterConfig = TypeAdapterConfig.GlobalSettings;
        mapsterConfig.Scan(Assembly.GetExecutingAssembly());
        services.AddSingleton(mapsterConfig);
        services.AddScoped<IMapper>(sp => new ObjectMapper(sp.GetRequiredService<TypeAdapterConfig>()));

        // Auth
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();
        services.AddScoped<IAuthService, AuthService.AuthService>();

        // Admin/staff directory — needed by InspectionCommandService/InspectionQueryService
        // (both consumer and admin APIs) to resolve SuperAdmins/staff for the inspection
        // hand-off flow, not just by the Admin API's own auth endpoints.
        services.AddScoped<IAdminAuthService, AdminAuthService>();

        // Email (Resend)
        services.AddHttpClient<ResendEmailService>();
        services.AddScoped<IEmailService, ResendEmailService>();

        // Geocoding (Nominatim/OpenStreetMap)
        services.AddHttpClient<NominatimGeocodingService>();
        services.AddScoped<IGeocodingService, NominatimGeocodingService>();

        services.AddScoped<ICustomerCommandService, CustomerCommandService>();
        services.AddScoped<ICustomerQueryService, CustomerQueryService>();
        services.AddScoped<IPropertyCommandService, PropertyCommandService>();
        services.AddScoped<IPropertyQueryService, PropertyQueryService>();
        services.AddScoped<ICustomerAddressCommandService, CustomerAddressCommandService>();
        services.AddScoped<ICustomerAddressQueryService, CustomerAddressQueryService>();
        services.AddScoped<IPropertyAddressQueryService, PropertyAddressQueryService>();
        services.AddScoped<IPropertyAddressCommandService, PropertyAddressCommandService>();
        services.AddScoped<IPropertyReportCommandService, PropertyReportCommandService>();
        services.AddScoped<IInspectionCommandService, InspectionCommandService>();
        services.AddScoped<IInspectionQueryService, InspectionQueryService>();
        services.AddScoped<INotificationCommandService, NotificationCommandService>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddScoped<IPropertyFileCommandService, PropertyFileCommandService>();
        services.AddScoped<IPropertyFileQueryService, PropertyFileQueryService>();
        services.AddScoped<IChatCommandService, ChatCommandService>();
        services.AddScoped<IChatQueryService, ChatQueryService>();
        services.AddSingleton<IUtilityService, UtilityService>();

        // AWS S3 File Storage
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(configuration["AWS:S3:Region"] ?? "af-south-1")
            };

            // On Lambda the credentials come from the execution role, so AccessKey/SecretKey
            // aren't in config. Passing the resulting nulls to AmazonS3Client threw a
            // NullReferenceException on the first upload (publish property, KYC/profile
            // uploads). Only use explicit keys when both are present, otherwise fall back
            // to the default credential chain — same pattern as the DynamoDB client.
            var accessKey = configuration["AWS:S3:AccessKey"];
            var secretKey = configuration["AWS:S3:SecretKey"];
            if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
                return new AmazonS3Client(accessKey, secretKey, config);

            return new AmazonS3Client(config);
        });
        services.AddSingleton<IFileStorageService, S3FileStorageService>();


        return services;
    }
}
