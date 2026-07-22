using System.Linq.Expressions;

namespace HousingHub.Data.RepositoryInterfaces.Queries;

public interface IGenericQueryRepository<T> where T : class
{
    /// <summary>
    /// Direct primary-key lookup (DynamoDB GetItem). Prefer this over
    /// GetByAsync(x => x.Id == id), which scans the whole table.
    /// </summary>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>
    /// Queries a global secondary index instead of scanning.
    /// Returns only the attributes the index projects — use GetByIdAsync to
    /// hydrate the full item when you need fields outside the projection.
    /// </summary>
    Task<IReadOnlyList<T>> QueryByIndexAsync(string indexName, object hashKeyValue);

    /// <summary>
    /// WARNING: scans the entire table and filters in memory. Fine for small
    /// tables and admin reporting; use GetByIdAsync/QueryByIndexAsync on hot paths.
    /// </summary>
    Task<T?> GetByAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> GetAllAsync();
    Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<T, bool>>? predicate = null);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
}
