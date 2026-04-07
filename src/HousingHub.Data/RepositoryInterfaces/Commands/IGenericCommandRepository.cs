using System.Linq.Expressions;

namespace HousingHub.Data.RepositoryInterfaces.Commands;

public interface IGenericCommandRepository<T> where T : class
{
    Task<bool> InsertAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);

    Task InsertRangeAsync(IEnumerable<T> entities);
    Task UpdateRangeAsync(IEnumerable<T> entities);
    Task DeleteRangeAsync(IEnumerable<T> entities);

    Task<T?> GetByIdAsync(Guid id);
    Task<T?> GetSingleAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> GetWhereAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null);
    Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null);
    Task<bool> ExistAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
}
