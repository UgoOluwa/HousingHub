using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;

namespace HousingHub.Repository.Commands;

public class GenericCommandRepository<T> : IGenericCommandRepository<T> where T : class
{
    private readonly AppDbContext _applicationContext;

    public GenericCommandRepository(AppDbContext applicationContext)
    {
        _applicationContext = applicationContext;
    }

    public async Task<bool> InsertAsync(T entity)
    {
        var response = await _applicationContext.Set<T>().AddAsync(entity);
        return response.State == EntityState.Added;
    }

    public void Update(T entity)
    {
        _applicationContext.Set<T>().Update(entity);
    }

    public void Delete(T entity)
    {
        _applicationContext.Set<T>().Remove(entity);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _applicationContext.SaveChangesAsync();
    }

    public async Task InsertRangeAsync(IEnumerable<T> entities)
    {
        await _applicationContext.Set<T>().AddRangeAsync(entities);
    }

    public void UpdateRange(IEnumerable<T> entities)
    {
        _applicationContext.Set<T>().UpdateRange(entities);
    }

    public void DeleteRange(IEnumerable<T> entities)
    {
        _applicationContext.Set<T>().RemoveRange(entities);
    }

    public async Task<bool> ExistAsync(Expression<Func<T, bool>> predicate)
    {
        return await _applicationContext.Set<T>().AnyAsync(predicate);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        return await _applicationContext.Set<T>().CountAsync(predicate);
    }

    public async Task<IEnumerable<T>> GetWhereAsync(Expression<Func<T, bool>> predicate)
    {
        return await _applicationContext.Set<T>().Where(predicate).ToListAsync();
    }

    public async Task<T?> GetSingleAsync(Expression<Func<T, bool>> predicate)
    {
        return await _applicationContext.Set<T>().FirstOrDefaultAsync(predicate);
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _applicationContext.Set<T>().FindAsync(id);
    }

    public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null ? await _applicationContext.Set<T>().ToListAsync() : await _applicationContext.Set<T>().Where(predicate).ToListAsync();
    }

    public async Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null ? await _applicationContext.Set<T>().ToListAsync() : await _applicationContext.Set<T>().Where(predicate).ToListAsync();
    }

    public IQueryable<T> GetQueryable(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null ? _applicationContext.Set<T>() : _applicationContext.Set<T>().Where(predicate);
    }
}
