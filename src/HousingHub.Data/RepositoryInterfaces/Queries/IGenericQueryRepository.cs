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
}
