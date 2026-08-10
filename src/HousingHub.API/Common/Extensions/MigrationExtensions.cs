using HousingHub.Data.Contexts;

namespace HousingHub.API.Common.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// Creates any missing DynamoDB tables, if table provisioning is switched on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off unless <c>Dynamo:AutoCreateTables</c> is true.</b> This runs before the
    /// app accepts traffic, and it calls ListTables — so on Lambda it added a DynamoDB
    /// round trip to <i>every</i> cold start, forever, in order to create tables that
    /// have existed since the first deploy. It also forced the execution role to carry
    /// <c>dynamodb:CreateTable</c> in production, a permission an API has no business
    /// holding.
    /// </para>
    /// <para>
    /// Leave it on for local development and integration tests, where starting against
    /// an empty DynamoDB Local is routine. In deployed environments the tables belong in
    /// your infrastructure definition, where they are reviewable and versioned.
    /// </para>
    /// </remarks>
    public static async Task InitializeDynamoDbAsync(this IApplicationBuilder app, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Dynamo:AutoCreateTables"))
            return;

        using IServiceScope scope = app.ApplicationServices.CreateScope();

        var initializer = scope.ServiceProvider.GetRequiredService<DynamoDbTableInitializer>();
        await initializer.InitializeAsync();
    }
}
