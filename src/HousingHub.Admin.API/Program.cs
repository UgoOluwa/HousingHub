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
using HousingHub.Core.Observability;
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
using Sentry;
using Sentry.Extensibility;
using Serilog;

namespace HousingHub.Admin.API;

public static class Program
{
    // No top-level await remains: schema initialisation is fire-and-forget, so
        // keeping this async would only raise CS1998.
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Fail fast on missing or placeholder secrets. Internal:WorkerSecret is the
        // only gate on PUT /api/Internal/admins/promote, which grants SuperAdmin — a
        // committed placeholder there is effectively a published password.
        RequiredSecrets.Validate(
            builder.Configuration,
            signingKeys: ["AdminJwt:Secret"],
            otherRequired:
            [
                "Internal:WorkerSecret",
                "Email:ResendApiKey",
                "AdminJwt:Issuer",
                "AdminJwt:Audience",
            ],
            requiredArrays: ["Cors:AllowedOrigins"]);

        var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

        // Error monitoring. Inert when Sentry:Dsn is unset. Use a SEPARATE Sentry
        // project from the consumer API so an admin outage is distinguishable from
        // a customer-facing one at a glance.
        builder.WebHost.UseSentry(options =>
        {
            SentryOptionsConfigurator.Configure(options, builder.Configuration);

            // Services here catch their own exceptions and return a failed
            // BaseResponse, so almost nothing reaches the pipeline as an unhandled
            // exception. Binding the event level to Error makes the existing
            // `_logger.LogError(ex, ...)` calls the reporting mechanism — without
            // this, Sentry would look installed and report almost nothing.
            options.MinimumEventLevel = LogLevel.Error;
            options.MinimumBreadcrumbLevel = LogLevel.Information;

            // Never attach the request body: it would carry KYC submissions and
            // login payloads verbatim, straight past the field-level scrubbing.
            options.MaxRequestBodySize = RequestSize.None;
        });

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
                .ConfigureContext(c =>
                {
                    // Prepended to every [DynamoDBTable] name, which is what lets one
                    // AWS account hold two environments. Empty in dev — the existing
                    // tables are unprefixed and giving them one would orphan the data.
                    // DynamoDbTableInitializer reads the same key through the same
                    // helper; see DynamoDbNaming for why that matters.
                    c.TableNamePrefix = DynamoDbNaming.TablePrefix(builder.Configuration);
                })
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

        // The API Gateway stage name doubles as a path prefix. Configuration rather
        // than a constant, so a second stage does not 404 every route — see the
        // matching note in the consumer API's Program.cs.
        var pathBase = builder.Configuration["Api:PathBase"];
        if (isLambda && !string.IsNullOrWhiteSpace(pathBase))
        {
            app.UsePathBase(pathBase.Trim());
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

                // UseExceptionHandler swallows the exception rather than rethrowing,
                // so Sentry's pipeline integration never sees it. Report explicitly.
                if (exceptionFeature?.Error is { } unhandled)
                {
                    SentrySdk.CaptureException(unhandled);
                }

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
            // Derived from the path base rather than hardcoded to "/admin". The docs
            // only serve locally, where there is no path base, so the hardcoded value
            // pointed the UI at a document that is not there — the page loaded and
            // the schema pane stayed empty.
            var docPath = $"{(isLambda ? pathBase?.TrimEnd('/') : null)}/openapi/v1.json";

            app.UseSwagger(c => c.RouteTemplate = "openapi/{documentName}.json");
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint(docPath, "HousingHub Admin API v1");
                c.RoutePrefix = "swagger";
            });
            app.MapScalarApiReference("/scalar", options =>
            {
                options.WithTitle("HousingHub Admin API")
                       .WithOpenApiRoutePattern(docPath);
            }).AllowAnonymous();
        }

        // Schema reconciliation, on by default and not awaited — see the consumer
        // API's MigrationExtensions for the reasoning. Both APIs share these tables;
        // whichever starts first does the work, and both doing it is harmless because
        // every step checks before it acts.
        if (builder.Configuration.GetValue("Dynamo:AutoCreateTables", defaultValue: true))
        {
            _ = Task.Run(async () =>
            {
                using var scope = app.Services.CreateScope();
                try
                {
                    var initializer = scope.ServiceProvider.GetRequiredService<DynamoDbTableInitializer>();
                    await initializer.InitializeAsync();
                }
                catch (Exception ex)
                {
                    // Must be caught: an unobserved exception in a fire-and-forget
                    // task is a process-level risk.
                    Log.Error(ex, "DynamoDB schema initialisation failed to run");
                }
            });
        }

        if (!isLambda)
        {
            app.UseHttpsRedirection();
        }

        app.UseCors();
        // Ahead of authentication deliberately — see the note in the consumer API's
        // Program.cs. It also matters more here: the internal worker endpoints are
        // anonymous and secret-gated, so the limiter is their only brute-force defence
        // and must run whether or not a token is present.
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        // Root redirect only makes sense where the docs are actually served.
        if (app.Environment.IsDevelopment())
        {
            // Relative, so it lands correctly whether or not a path base is applied.
            app.MapGet("/", () => Results.Redirect("scalar")).AllowAnonymous();
        }

        app.MapControllers();

        app.Run();
    }
}
