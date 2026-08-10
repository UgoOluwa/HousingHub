using System.Reflection;
using System.Text;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using HousingHub.Admin.API.Common;
using HousingHub.Admin.API.Realtime;
using HousingHub.Application;
using HousingHub.Core.Configuration;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.Contexts;
using HousingHub.Repository;
using HousingHub.Service;
using HousingHub.Service.AdminService;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Commons.Web;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.NotificationService.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

namespace HousingHub.Admin.API;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Fail fast on missing or placeholder secrets. Internal:WorkerSecret is the
        // only gate on PUT /api/Internal/admins/promote, which grants SuperAdmin — a
        // committed placeholder there is effectively a published password.
        RequiredSecrets.Validate(
            builder.Configuration,
            signingKeys: ["AdminJwt:Secret"],
            otherRequired: ["Internal:WorkerSecret", "Email:ResendApiKey"]);

        var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

        builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console());

        builder.Services.AddAdminRateLimiting();

        builder.Services.AddControllers(options =>
        {
            // Bounds page size everywhere at once — see the filter for why this is
            // global rather than per-endpoint.
            options.Filters.Add<PaginationClampFilter>();
        });
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

            // Staff management (add/deactivate/reactivate/view all) is restricted to
            // SuperAdmins — see AdminAccountController's staff endpoints.
            options.AddPolicy("SuperAdminOnly", policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("adminRole", "SuperAdmin"));
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
        builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();

        // Chat (IChatCommandService/IChatQueryService, registered generically by
        // AddInjectionService below) needs these — the Admin API has no SignalR hub
        // of its own, so real-time push is a no-op here (see NoOpRealtimeNotifiers).
        builder.Services.AddSingleton<IChatRealtimeNotifier, NoOpChatRealtimeNotifier>();
        builder.Services.AddSingleton<IRealtimeNotifier, NoOpRealtimeNotifier>();

        // Shared application + repository layers
        builder.Services.AddInjectionRepository()
            .AddInjectionService()
            .AddInjectionApplication();

        var app = builder.Build();

        if (isLambda)
        {
            app.UsePathBase("/admin");
        }

        // Turns an unhandled exception into a normal JSON error response instead of a
        // bare Lambda 500 with no body — any exception that escapes a controller's own
        // try/catch previously reached the caller with no diagnostic information at all.
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                Log.Error(exceptionFeature?.Error, "Unhandled exception in Admin API");

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new BaseResponse<object?>(
                    null, false, "500", "An unexpected error occurred. Please try again."));
            });
        });

        // Development only. These were previously served in production with
        // .AllowAnonymous(), publishing the entire admin surface — every DTO shape and
        // the SuperAdmin-only routes — to unauthenticated callers. The consumer API
        // already gates its docs this way.
        if (app.Environment.IsDevelopment())
        {
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
        }

        using (var scope = app.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DynamoDbTableInitializer>();
            await initializer.InitializeAsync();
        }

        if (!isLambda)
        {
            app.UseHttpsRedirection();
        }

        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        // Root redirect only makes sense where the docs are actually served.
        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/", () => Results.Redirect("/admin/scalar")).AllowAnonymous();
        }

        app.MapControllers();

        app.Run();
    }
}
