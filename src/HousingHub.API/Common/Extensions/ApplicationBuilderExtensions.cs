using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace HousingHub.API.Common.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseDocWithUi(this WebApplication app)
    {
        app.UseSwagger(options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        });
        app.UseSwaggerUI(
            options =>
            {
                var descriptions = app.DescribeApiVersions();

                // build a swagger endpoint for each discovered API version
                foreach (var description in descriptions.Select(x => x.GroupName))
                {
                    var url = $"/swagger/{description}/swagger.json";
                    var name = description.ToUpperInvariant();
                    options.SwaggerEndpoint(url, name);
                }
            }
        );

        app.MapSwagger("/openapi/{documentName}.json");
        app.MapScalarApiReference();

        return app;
    }
}
