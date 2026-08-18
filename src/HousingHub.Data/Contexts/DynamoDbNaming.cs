using Microsoft.Extensions.Configuration;

namespace HousingHub.Data.Contexts;

/// <summary>
/// Resolves the prefix applied to every DynamoDB table name, so one AWS account
/// can hold more than one environment.
/// </summary>
/// <remarks>
/// <para>
/// Table names are declared as compile-time constants — <c>[DynamoDBTable("Customers")]</c>
/// — so they cannot vary per environment on their own. The SDK's
/// <c>TableNamePrefix</c> is what makes them vary: set it to <c>prod_</c> and the
/// same attribute resolves to <c>prod_Customers</c>.
/// </para>
/// <para>
/// <b>This exists as a shared helper for one reason.</b> Two separate pieces of
/// code have to agree on the prefix: the <c>IDynamoDBContext</c> that reads and
/// writes rows, and <see cref="DynamoDbTableInitializer"/> that creates the
/// tables. If they are configured independently, it is entirely possible to set
/// one and forget the other — and the failure mode is quiet and confusing. The
/// initializer reports creating every table successfully, and every query then
/// comes back empty against tables that do exist, under names nothing reads.
/// Reading the same key through the same function makes that mismatch impossible.
/// </para>
/// <para>
/// <b>Empty is a legitimate value and the default.</b> The existing environment's
/// tables are unprefixed, and giving it a prefix now would orphan every row
/// already stored. Dev stays unprefixed; production is the one that opts in.
/// </para>
/// </remarks>
public static class DynamoDbNaming
{
    /// <summary>
    /// Configuration key holding the prefix. As an environment variable this is
    /// <c>Dynamo__TablePrefix</c> — double underscore, since Lambda environment
    /// variable names cannot contain a colon.
    /// </summary>
    public const string TablePrefixKey = "Dynamo:TablePrefix";

    /// <summary>
    /// The configured prefix, or an empty string when none is set.
    /// </summary>
    /// <remarks>
    /// Trimmed, because this arrives from a Lambda environment variable and a
    /// stray space would silently produce a table named <c>" prod_Customers"</c>
    /// — valid as far as DynamoDB is concerned, and impossible to spot in a
    /// console listing.
    /// </remarks>
    public static string TablePrefix(IConfiguration configuration) =>
        configuration[TablePrefixKey]?.Trim() ?? string.Empty;
}
