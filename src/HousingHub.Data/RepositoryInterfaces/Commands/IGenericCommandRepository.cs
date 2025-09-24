using System.Linq.Expressions;

namespace HousingHub.Data.RepositoryInterfaces.Commands;

public interface IGenericCommandRepository<T> where T : class
{
    Task<bool> InsertAsync(T entity);
    void Update(T entity);
    void Delete(T entity);

    Task<int> SaveChangesAsync();

    Task InsertRangeAsync(IEnumerable<T> entities);
    void UpdateRange(IEnumerable<T> entities);
    void DeleteRange(IEnumerable<T> entities);
    Task<bool> ExistAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);

    Task<IEnumerable<T>> GetWhereAsync(Expression<Func<T, bool>> predicate);
    Task<T?> GetSingleAsync(Expression<Func<T, bool>> predicate);
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null);
    Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null);
    IQueryable<T> GetQueryable(Expression<Func<T, bool>>? predicate = null);
}
