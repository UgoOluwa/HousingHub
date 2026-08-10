using System.Linq.Expressions;

namespace HousingHub.Data.RepositoryInterfaces.Queries;

public interface IGenericQueryRepository<T> where T : class
{
    /// <summary>Direct primary-key lookup (DynamoDB GetItem).</summary>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>
    /// Queries a global secondary index instead of scanning. Every index on these
    /// tables is created with ProjectionType.ALL, so the full item is returned.
    /// </summary>
    Task<IReadOnlyList<T>> QueryByIndexAsync(string indexName, object hashKeyValue);

    /// <summary>
    /// Finds the first row matching the predicate.
    /// </summary>
    /// <remarks>
    /// The implementation narrows the read automatically where it can: an equality
    /// on the primary key becomes a GetItem, an equality on a GSI hash key becomes a
    /// Query. A predicate with no indexable equality — an OR across two columns, a
    /// range comparison, a method call — still scans the table, so prefer a shape
    /// that can be indexed on hot paths.
    ///
    /// Note that a narrowed read returns rows in index order rather than table
    /// order, so "first" may differ from before when more than one row matches.
    /// Add an explicit ordering if which row you get matters.
    /// </remarks>
    Task<T?> GetByAsync(Expression<Func<T, bool>> predicate);

    /// <inheritdoc cref="GetByAsync" />
    Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> predicate);

    /// <summary>Every row. Always a full scan — there is nothing to narrow by.</summary>
    Task<IEnumerable<T>> GetAllAsync();

    /// <inheritdoc cref="GetByAsync" />
    Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<T, bool>>? predicate = null);

    /// <inheritdoc cref="GetByAsync" />
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

    /// <inheritdoc cref="GetByAsync" />
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Loads every row whose <paramref name="keySelector"/> property equals one of
    /// <paramref name="values"/>. Use this instead of
    /// <c>GetAllAsync(x =&gt; values.Contains(x.Key))</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Contains</c> reads as the efficient choice — one call instead of N — but it
    /// is a method call, so the predicate cannot be narrowed to an index and the
    /// repository falls through to a full table scan. Twenty targeted reads get
    /// replaced by one read of everything, which looks like an optimisation right up
    /// until the table is large enough for it to matter.
    /// </para>
    /// <para>
    /// This issues one GetItem (primary key) or one Query (GSI hash key) per distinct
    /// value, in parallel, so the wall-clock cost is roughly a single round trip and
    /// the read cost is proportional to what is actually returned. If the property is
    /// neither, it falls back to the scan — same result, no worse than before.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<T>> GetManyByAsync<TKey>(
        Expression<Func<T, TKey>> keySelector,
        IEnumerable<TKey> values);
}
