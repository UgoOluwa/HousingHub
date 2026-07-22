using System.Text;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Asp.Versioning;
using HealthChecks.UI.Client;
using HousingHub.API.Common;
using HousingHub.API.Common.Extensions;
using HousingHub.API.Common.Middlewares;
using HousingHub.API.Hubs;
using HousingHub.Application;
using HousingHub.Data.Contexts;
using HousingHub.Model.Enums;
using HousingHub.Repository;
using HousingHub.Service;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.ChatService.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HousingHub.API
{
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

            // Add services to the container.

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

            builder.Services.AddHealthChecks();
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            builder.Services.AddSwaggerGen(options =>
            {
                // add a custom operation filter which sets default values
                options.OperationFilter<SwaggerDefaultValues>();

                // Required in Swashbuckle 10.x: explicitly map IFormFile for [FromForm] file uploads
                options.MapType<IFormFile>(() => new Microsoft.OpenApi.OpenApiSchema
                {
                    Type = Microsoft.OpenApi.JsonSchemaType.String,
                    Format = "binary"
                });
            });

            builder.Services.AddApiVersioning(option =>
            {
                option.AssumeDefaultVersionWhenUnspecified = true; //This ensures if client doesn't specify an API version. The default version should be considered. 
                option.DefaultApiVersion = new ApiVersion(1, 0); //This we set the default API version
                option.ReportApiVersions = true; //The allow the API Version information to be reported in the client  in the response header. This will be useful for the client to understand the version of the API they are interacting with.

            })
            .AddMvc()
            .AddApiExplorer(options => {
                options.GroupNameFormat = "'v'VVV"; //The say our format of our version number ��v�major[.minor][-status]�
                options.SubstituteApiVersionInUrl = true; //This will help us to resolve the ambiguity when there is a routing conflict due to routing template one or more end points are same.
            });

            builder.Services.AddAuthorization(options =>
            {
                // Property owners, agents and developers all manage listings.
                // Keep in sync with CustomerTypeExtensions.CanManageProperties().
                options.AddPolicy("PropertyOwnerOrAgent", policy =>
                    policy.RequireAssertion(context =>
                    {
                        var claim = context.User.FindFirst("customer_type")?.Value;
                        if (string.IsNullOrEmpty(claim)) return false;

                        return Enum.TryParse<CustomerType>(claim, ignoreCase: true, out var customerType)
                               && customerType.CanManageProperties();
                    }));
                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireAssertion(context =>
                    {
                        var customerType = context.User.FindFirst("customer_type")?.Value;
                        return customerType == "Admin";
                    }));
            });
            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(o =>
                {
                    o.RequireHttpsMetadata = false;
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ClockSkew = TimeSpan.Zero
                    };
                    o.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                })
                .AddCookie("ExternalAuth", o =>
                {
                    o.Cookie.SameSite = SameSiteMode.Lax;
                    o.Cookie.HttpOnly = true;
                    o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                })
                .AddGoogle(GoogleDefaults.AuthenticationScheme, o =>
                {
                    o.ClientId = builder.Configuration["Google:ClientId"]!;
                    o.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
                    o.SignInScheme = "ExternalAuth";
                    o.SaveTokens = true;

                    // Not mapped by default, but required before we link a Google
                    // identity onto an existing account.
                    o.ClaimActions.MapJsonKey("email_verified", "email_verified", "boolean");
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

            // SignalR (disabled under Lambda — no persistent WebSocket support)
            if (!isLambda)
            {
                builder.Services.AddSignalR();
                builder.Services.AddSingleton<IUserIdProvider, NameUserIdProvider>();
                builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotificationSender>();
                builder.Services.AddSingleton<IChatRealtimeNotifier, SignalRChatNotifier>();
            }
            else
            {
                builder.Services.AddSingleton<IRealtimeNotifier, NoOpRealtimeNotifier>();
                builder.Services.AddSingleton<IChatRealtimeNotifier, NoOpChatRealtimeNotifier>();
            }

            //Add methods Extensions
            builder.Services.AddInjectionRepository()
                .AddInjectionService()
                .AddInjectionApplication();


            builder.Services.AddTransient<ExceptionHandlingMiddleware>();

            var app = builder.Build();

            if (isLambda)
            {
                app.UsePathBase("/dev");
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseDocWithUi();
            }

            await app.InitializeDynamoDbAsync();

            app.MapHealthChecks("health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            if (!isLambda)
            {
                app.UseHttpsRedirection();
            }
            app.UseAppExceptionMiddleware();

            app.UseCors();

            app.UseAuthentication();

            app.UseAuthorization();
            

            app.MapControllers();
            if (!isLambda)
            {
                app.MapHub<NotificationHub>("/hubs/notifications");
                app.MapHub<ChatHub>("/hubs/chat");
            }

            app.Run();
        }
    }
}
