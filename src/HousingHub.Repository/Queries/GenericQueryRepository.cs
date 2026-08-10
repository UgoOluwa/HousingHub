using System.Linq.Expressions;
using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;

namespace HousingHub.Repository.Queries;

/// <summary>
/// Predicate-based reads over DynamoDB.
/// </summary>
/// <remarks>
/// <para>
/// Every method here used to satisfy its predicate by scanning the entire table and
/// filtering in memory. That made read cost proportional to table size rather than
/// result size — the single largest driver of both latency and RCU spend — and it
/// meant an unauthenticated endpoint like <c>GET /api/v1/Property/all</c> could be
/// used to burn read capacity on demand.
/// </para>
/// <para>
/// Now each read first tries to narrow itself:
/// </para>
/// <list type="number">
///   <item>an equality on the table's hash key becomes a GetItem — one row, constant cost;</item>
///   <item>an equality on a GSI hash key becomes a Query against that index;</item>
///   <item>anything else still falls back to a scan.</item>
/// </list>
/// <para>
/// <b>The original predicate is always re-applied to whatever comes back.</b> The
/// index only shrinks the candidate set, so results are identical to the previous
/// behaviour and a missed optimisation costs performance, never correctness.
/// </para>
/// </remarks>
public partial class GenericQueryRepository<T> : IGenericQueryRepository<T> where T : class
{
    protected readonly IDynamoDBContext _context;

    public GenericQueryRepository(IDynamoDBContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        // GetItem on the hash key — a single-digit-millisecond read regardless of
        // table size, versus scanning every row to find one.
        return await _context.LoadAsync<T>(id);
    }

    public async Task<IReadOnlyList<T>> QueryByIndexAsync(string indexName, object hashKeyValue)
    {
        var search = _context.QueryAsync<T>(hashKeyValue, new QueryConfig { IndexName = indexName });
        return await search.GetRemainingAsync();
    }

    public async Task<T?> GetByAsync(Expression<Func<T, bool>> predicate)
    {
        var candidates = await NarrowAsync(predicate);
        return candidates.AsQueryable().FirstOrDefault(predicate);
    }

    public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> predicate)
    {
        var candidates = await NarrowAsync(predicate);
        return candidates.AsQueryable().Where(predicate).ToList();
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        // No predicate to narrow by — this genuinely is "every row".
        return await ScanAllAsync();
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        var candidates = await NarrowAsync(predicate);
        return candidates.AsQueryable().Any(predicate);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        var candidates = await NarrowAsync(predicate);
        return candidates.AsQueryable().Count(predicate);
    }

    public async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, Expression<Func<T, bool>>? predicate = null)
    {
        var candidates = predicate is null
            ? await ScanAllAsync()
            : await NarrowAsync(predicate);

        IQueryable<T> query = candidates.AsQueryable();

        if (predicate != null)
            query = query.Where(predicate);

        var totalCount = query.Count();
        var pagedItems = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (pagedItems, totalCount);
    }

    /// <summary>
    /// Returns the smallest set of rows guaranteed to contain every match for the
    /// predicate. Callers must still apply the predicate themselves.
    /// </summary>
    private async Task<IReadOnlyList<T>> NarrowAsync(Expression<Func<T, bool>> predicate)
    {
        var candidates = EqualityPredicateExtractor.Extract(predicate);

        foreach (var candidate in candidates)
        {
            if (!DynamoIndexMap<T>.IsQueryableKeyValue(candidate.Value)) continue;

            // Best case: the predicate pins the primary key, so this is one row.
            if (DynamoIndexMap<T>.HashKey?.Name == candidate.PropertyName)
            {
                var item = await _context.LoadAsync<T>(candidate.Value!);
                return item is null ? [] : [item];
            }

            if (DynamoIndexMap<T>.GlobalIndexes.TryGetValue(candidate.PropertyName, out var indexName)
                && !IndexKnownMissing(indexName))
            {
                try
                {
                    return await QueryByIndexAsync(indexName, candidate.Value!);
                }
                catch (Exception ex) when (IsMissingIndex(ex))
                {
                    // The entity declares this GSI but the deployed table does not have
                    // it. DynamoDbTableInitializer only creates tables that do not
                    // already exist, so an index added to the code after a table was
                    // created is absent in that environment.
                    //
                    // Degrade to the previous behaviour rather than failing the request:
                    // slower, but identical results. Remembered per process so the cost
                    // is one failed call, not one per request.
                    RememberMissingIndex(indexName);
                }
            }
        }

        // Nothing indexable in this predicate. Remaining scans are visible in the
        // DynamoDB ConsumedReadCapacity metrics per table if they need hunting down.
        return await ScanAllAsync();
    }

    /// <summary>
    /// Indexes the code declares but the deployed table turned out not to have.
    /// Keyed by entity and index so one missing index does not disable the others.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> MissingIndexes = new();

    private static bool IndexKnownMissing(string indexName) =>
        MissingIndexes.ContainsKey($"{typeof(T).Name}:{indexName}");

    private static void RememberMissingIndex(string indexName) =>
        MissingIndexes[$"{typeof(T).Name}:{indexName}"] = true;

    /// <summary>
    /// Deliberately narrow. Matching any exception mentioning "index" would swallow
    /// unrelated validation errors into a permanent silent scan for the process
    /// lifetime, which is far worse than the error it was meant to tolerate.
    /// </summary>
    private static bool IsMissingIndex(Exception ex) =>
        ex is Amazon.DynamoDBv2.Model.ResourceNotFoundException
        || (ex is Amazon.DynamoDBv2.AmazonDynamoDBException { ErrorCode: "ValidationException" } dynamoEx
            && dynamoEx.Message.Contains("specified index", StringComparison.OrdinalIgnoreCase));

    private async Task<List<T>> ScanAllAsync()
    {
        var search = _context.ScanAsync<T>(new List<ScanCondition>());
        return await search.GetRemainingAsync();
    }
}
