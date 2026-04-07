using HousingHub.Data.Contexts;

namespace HousingHub.API.Common.Extensions;

public static class MigrationExtensions
{
    public static async Task InitializeDynamoDbAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        var initializer = scope.ServiceProvider.GetRequiredService<DynamoDbTableInitializer>();
        await initializer.InitializeAsync();
    }
}
