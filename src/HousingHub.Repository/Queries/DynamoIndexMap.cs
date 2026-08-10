using System.Reflection;
using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Repository.Queries;

/// <summary>
/// Discovers which properties of an entity are queryable without a table scan.
/// </summary>
/// <remarks>
/// Read from the DynamoDB attributes already declared on the entities rather than a
/// hand-maintained list, so adding a GSI to an entity makes it usable immediately
/// and there is no second source of truth to drift out of sync.
///
/// Reflection runs once per type and is cached.
/// </remarks>
internal static class DynamoIndexMap<T> where T : class
{
    /// <summary>Property backing the table's own hash key, if any. Reachable by GetItem.</summary>
    public static readonly PropertyInfo? HashKey;

    /// <summary>Property name to GSI name, for properties that are a GSI hash key.</summary>
    public static readonly IReadOnlyDictionary<string, string> GlobalIndexes;

    static DynamoIndexMap()
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // DynamoDBGlobalSecondaryIndexHashKeyAttribute DERIVES FROM
        // DynamoDBHashKeyAttribute, so GetCustomAttribute<DynamoDBHashKeyAttribute>()
        // matches GSI properties too. The real hash key (Id) is inherited from
        // BaseEntity while GSI properties are declared on the derived type, and
        // GetProperties returns derived members first — so without the second clause
        // every entity resolves its "hash key" to a GSI property instead.
        //
        // The consequences were silent: a Guid-typed GSI property produced a GetItem
        // against the wrong key and returned nothing at all, emptying notifications,
        // chat history and property images rather than failing loudly.
        HashKey = properties.FirstOrDefault(p =>
            p.GetCustomAttribute<DynamoDBHashKeyAttribute>() is not null
            && p.GetCustomAttribute<DynamoDBGlobalSecondaryIndexHashKeyAttribute>() is null);

        var indexes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<DynamoDBGlobalSecondaryIndexHashKeyAttribute>();
            if (attribute is null) continue;

            // The attribute can name several indexes; the first is enough to query by.
            var indexName = attribute.IndexNames?.FirstOrDefault();
            if (!string.IsNullOrEmpty(indexName))
                indexes[property.Name] = indexName;
        }

        GlobalIndexes = indexes;
    }

    /// <summary>Types DynamoDB can use as a key. Anything else cannot be queried directly.</summary>
    private static readonly HashSet<Type> QueryableKeyTypes =
    [
        typeof(string), typeof(Guid), typeof(int), typeof(long), typeof(decimal), typeof(double),
    ];

    public static bool IsQueryableKeyValue(object? value)
    {
        if (value is null) return false;

        var type = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();
        if (!QueryableKeyTypes.Contains(type)) return false;

        // An empty string or default Guid is never a real key, and querying on one
        // would either error or quietly return nothing.
        return value switch
        {
            string s => !string.IsNullOrWhiteSpace(s),
            Guid g => g != Guid.Empty,
            _ => true,
        };
    }
}
