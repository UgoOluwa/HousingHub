using System.Reflection;
using System.Text;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using HousingHub.Application;
using HousingHub.Data.Contexts;
using HousingHub.Repository;
using HousingHub.Service;
using HousingHub.Service.AdminService;
using HousingHub.Service.Commons.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

namespace HousingHub.Admin.API;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

        builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console());

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
            {
                Title = "HousingHub Admin API",
                Version = "v1",
                Description = "Internal administration API — requires Admin JWT bearer token."
            });

            // Include XML doc comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath);
        });

        // Admin JWT — completely separate secret from customer API
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(builder.Configuration["AdminJwt:Secret"]!)),
                    ValidIssuer = builder.Configuration["AdminJwt:Issuer"],
                    ValidAudience = builder.Configuration["AdminJwt:Audience"],
                    ClockSkew = TimeSpan.Zero
                };
            });

        // All endpoints require Admin role by default — login opts out via [AllowAnonymous]
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("role", "Admin")
                .Build();
        });

        // DynamoDB
        builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var config = new AmazonDynamoDBConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(
                    builder.Configuration["AWS:DynamoDB:Region"] ?? "us-east-1")
            };

            var serviceUrl = builder.Configuration["AWS:DynamoDB:ServiceURL"];
            if (!string.IsNullOrEmpty(serviceUrl))
                config.ServiceURL = serviceUrl;

            var accessKey = builder.Configuration["AWS:S3:AccessKey"];
            var secretKey = builder.Configuration["AWS:S3:SecretKey"];
            if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
                return new AmazonDynamoDBClient(new BasicAWSCredentials(accessKey, secretKey), config);

            return new AmazonDynamoDBClient(config);
        });
        builder.Services.AddSingleton<IDynamoDBContext>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonDynamoDB>();
            return new DynamoDBContextBuilder()
                .WithDynamoDBClient(() => client)
                .Build();
        });
        builder.Services.AddTransient<DynamoDbTableInitializer>();

        // Admin-specific services
        builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
        builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
        builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();

        // Shared application + repository layers
        builder.Services.AddInjectionRepository()
            .AddInjectionService()
            .AddInjectionApplication();

        var app = builder.Build();

        if (isLambda)
        {
            app.UsePathBase("/admin");
        }

        app.UseSwagger(c => c.RouteTemplate = "openapi/{documentName}.json");
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/admin/openapi/v1.json", "HousingHub Admin API v1");
            c.RoutePrefix = "swagger";
        });
        app.MapScalarApiReference("/scalar", options =>
        {
            options.WithTitle("HousingHub Admin API")
                   .WithOpenApiRoutePattern("/admin/openapi/v1.json");
        }).AllowAnonymous();

        using (var scope = app.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DynamoDbTableInitializer>();
            await initializer.InitializeAsync();
        }

        if (!isLambda)
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/", () => Results.Redirect("/admin/scalar")).AllowAnonymous();
        app.MapControllers();

        app.Run();
    }
}
