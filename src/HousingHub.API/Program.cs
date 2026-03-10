using System.Text;
using Asp.Versioning;
using HealthChecks.UI.Client;
using HousingHub.API.Common;
using HousingHub.API.Common.Extensions;
using HousingHub.API.Common.Middlewares;
using HousingHub.Application;
using HousingHub.Repository;
using HousingHub.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HousingHub.API
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console());

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            builder.Services.AddSwaggerGen(options =>
            {
                // add a custom operation filter which sets default values
                options.OperationFilter<SwaggerDefaultValues>();
            });

            builder.Services.AddApiVersioning(option =>
            {
                option.AssumeDefaultVersionWhenUnspecified = true; //This ensures if client doesn't specify an API version. The default version should be considered. 
                option.DefaultApiVersion = new ApiVersion(1, 0); //This we set the default API version
                option.ReportApiVersions = true; //The allow the API Version information to be reported in the client  in the response header. This will be useful for the client to understand the version of the API they are interacting with.

            })
            .AddMvc()
            .AddApiExplorer(options => {
                options.GroupNameFormat = "'v'VVV"; //The say our format of our version number “‘v’major[.minor][-status]”
                options.SubstituteApiVersionInUrl = true; //This will help us to resolve the ambiguity when there is a routing conflict due to routing template one or more end points are same.
            });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("PropertyOwnerOrAgent", policy =>
                    policy.RequireAssertion(context =>
                    {
                        var customerType = context.User.FindFirst("customer_type")?.Value;
                        if (string.IsNullOrEmpty(customerType)) return false;
                        return customerType.Contains("HouseOwner") || customerType.Contains("Agent");
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
                });

            //Add methods Extensions
            builder.Services.AddInjectionRepository()
                .AddInjectionService()
                .AddInjectionApplication();


            builder.Services.AddTransient<ExceptionHandlingMiddleware>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseDocWithUi();

                app.ApplyMigrations();               
            }

            app.MapHealthChecks("health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            app.UseHttpsRedirection();
            app.UseAppExceptionMiddleware();

            app.UseAuthentication();

            app.UseAuthorization();
            

            app.MapControllers();

            app.Run();
        }
    }
}
