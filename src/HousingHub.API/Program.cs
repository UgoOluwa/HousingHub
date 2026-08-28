using Microsoft.AspNetCore.Mvc;
using HousingHub.Core.CustomResponses;
using System.Text;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Asp.Versioning;
using HealthChecks.UI.Client;
using HousingHub.API.Common;
using HousingHub.API.Common.Extensions;
using HousingHub.API.Hubs;
using HousingHub.Application;
using HousingHub.Application.Commons.Web;
using HousingHub.Core.Configuration;
using HousingHub.Core.Observability;
using HousingHub.Data.Contexts;
using HousingHub.Model.Enums;
using HousingHub.Repository;
using HousingHub.Service;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Commons.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Sentry;
using Sentry.Extensibility;
using Serilog;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HousingHub.API
{
    public static class Program
    {
        // No top-level await remains: schema initialisation is fire-and-forget, so
        // keeping this async would only raise CS1998.
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Refuse to start on a missing or placeholder secret rather than booting
            // with a value that is committed to source control. Runs before anything
            // reads configuration, so a misconfigured deploy fails immediately and
            // loudly instead of silently signing forgeable tokens.
            RequiredSecrets.Validate(
                builder.Configuration,
                signingKeys: ["Jwt:Secret"],
                // Issuer and audience are validated on every incoming token. Left unset
                // they don't loosen validation — they break it, since ValidateIssuer
                // defaults to true and has nothing to compare against. Every request
                // then 401s with no explanation.
                otherRequired:
                [
                    "Email:ResendApiKey",
                    "Google:ClientSecret",
                    "Google:ClientId",
                    "Jwt:Issuer",
                    "Jwt:Audience",
                ],
                requiredArrays: ["Cors:AllowedOrigins"]);

            var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

            // Error monitoring. Inert when Sentry:Dsn is unset, so this is safe to
            // ship before the DSN exists. See SentryOptionsConfigurator for what is
            // scrubbed and why the free plan's quota shapes the filtering.
            builder.WebHost.UseSentry(options =>
            {
                SentryOptionsConfigurator.Configure(options, builder.Configuration);

                // THIS is what makes Sentry see anything at all in this codebase.
                //
                // Almost every service here catches its own exceptions, logs them and
                // returns a failed BaseResponse — so the exception never propagates to
                // the pipeline and an integration relying only on unhandled exceptions
                // would report a near-empty stream while the app quietly failed. Wiring
                // the event level to Error means `_logger.LogError(ex, ...)`, which the
                // codebase already calls everywhere, becomes the reporting mechanism.
                //
                // Set explicitly rather than left to the default so that changing it is
                // a deliberate act: raising it to Critical would silently switch
                // monitoring off for the entire application.
                options.MinimumEventLevel = LogLevel.Error;

                // Information-level logs ride along as breadcrumbs on events that were
                // being sent anyway. They cost no additional quota and are usually the
                // difference between a stack trace and an explanation.
                options.MinimumBreadcrumbLevel = LogLevel.Information;

                // Never attach the request body: it would carry KYC submissions and
                // login payloads verbatim, straight past the field-level scrubbing.
                options.MaxRequestBodySize = RequestSize.None;
            });

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

            builder.Services.AddAppRateLimiting();
            builder.Services.AddHealthChecks();
            builder.Services.AddControllers(options =>
            {
                // Bounds page size everywhere at once — see the filter for why this is
                // global rather than per-endpoint.
                options.Filters.Add<PaginationClampFilter>();
            });
            // Model-state failures are shaped into the same envelope as everything else.
            //
            // These are rejected before the action runs, so ValidationBehaviour and the
            // exception middleware never see them. Without this the response is the
            // framework's ValidationProblemDetails, which has no `message` field — and a
            // client looking for one falls through to whatever its HTTP library says,
            // which is how a user came to be shown "Request failed with status code 400".
            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var failures = context.ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .Select(entry => new KeyValuePair<string, string[]>(
                            entry.Key,
                            entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray()))
                        .ToList();

                    var body = new BaseErrorResponse(
                        ValidationMessageFormatter.DescribeEach(failures).ToHashSet(),
                        StatusCodes.Status400BadRequest.ToString(),
                        ValidationMessageFormatter.Summarise(failures));

                    return new BadRequestObjectResult(body);
                };
            });

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
                // Deny by default. Every endpoint requires an authenticated user unless it
                // explicitly opts out with [AllowAnonymous].
                //
                // This inverts the previous default of "open unless secured", which is how
                // GET /Customer/all, GET /Customer/{id} and DELETE /Customer/{id} ended up
                // reachable by any signed-in user. A missing [Authorize] is now a closed
                // door rather than an open one.
                //
                // The genuinely public surface is: all of AuthController, the public
                // property reads (all/{id}/new/trending/nearby/{id}/files), the
                // PropertyAddress reads, FaqController, UtilityController and /health.
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

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
                    .ConfigureContext(c =>
                    {
                        // Prepended to every [DynamoDBTable] name, which is what lets
                        // one AWS account hold two environments. Empty in dev — the
                        // existing tables are unprefixed and giving them one would
                        // orphan the data. DynamoDbTableInitializer reads the same key
                        // through the same helper; see DynamoDbNaming for why.
                        c.TableNamePrefix = DynamoDbNaming.TablePrefix(builder.Configuration);
                    })
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

            // The API Gateway stage name doubles as a path prefix, so the app has to
            // know it. It was hardcoded to "/dev" — a stage name compiled into the
            // application, which meant a second stage 404'd every route. It is also
            // where the CSP bug came from: the value looks like a base URL and gets
            // used as one, but a CSP source carrying a path matches only that exact
            // path.
            //
            // Empty or unset means no path base, which is correct outside Lambda.
            var pathBase = builder.Configuration["Api:PathBase"];
            if (isLambda && !string.IsNullOrWhiteSpace(pathBase))
            {
                app.UsePathBase(pathBase.Trim());
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseDocWithUi();
            }

            app.InitializeDynamoDb(builder.Configuration);

            // Must stay anonymous: the deny-by-default FallbackPolicy applies to every
            // routed endpoint, including this one. Without the opt-out, load balancer
            // and container health probes receive 401 and mark the target unhealthy.
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            }).AllowAnonymous();

            if (!isLambda)
            {
                app.UseHttpsRedirection();
            }
            app.UseAppExceptionMiddleware();

            app.UseCors();

            // Ahead of authentication deliberately. Validating a JWT signature is real
            // CPU work, and doing it before deciding whether to throttle means a flood
            // of forged tokens is paid for in full before being rejected. The limiter
            // partitions on IP, not identity, so it has everything it needs this early.
            app.UseRateLimiter();

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
