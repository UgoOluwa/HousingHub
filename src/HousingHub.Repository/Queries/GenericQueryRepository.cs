using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Service.RepositoryInterfaces.Common;

namespace HousingHub.Repository.Queries;

public partial class GenericQueryRepository<T> : IGenericQueryRepository<T> where T : class
{
    private readonly AppDbContext _applicationContext;

    public GenericQueryRepository(AppDbContext applicationContext)
    {
        _applicationContext = applicationContext;
    }
    
    public async Task<T?> GetByAsync(Expression<Func<T, bool>> predicate, FindOptions? findOptions = null)
    {
        return await Get(findOptions).FirstOrDefaultAsync(predicate)!;
    }
    public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> predicate, FindOptions? findOptions = null)
    {
        return await Get(findOptions).Where(predicate).ToListAsync();
    }
    public async Task<IEnumerable<T>> GetAllAsync(FindOptions? findOptions = null)
    {
        return await Get(findOptions).ToListAsync();
    }
    
    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await _applicationContext.Set<T>().AnyAsync(predicate);
    }
    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        return await _applicationContext.Set<T>().CountAsync(predicate);
    }

    public async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<T, bool>>? predicate = null, FindOptions? findOptions = null)
    {
        IQueryable<T> query = Get(findOptions);

        if (predicate != null)
            query = query.Where(predicate);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    private DbSet<T> Get(FindOptions? findOptions = null)
    {
        findOptions ??= new FindOptions();
        var entity = _applicationContext.Set<T>();
        if (findOptions.IsAsNoTracking && findOptions.IsIgnoreAutoIncludes)
        {
            entity.IgnoreAutoIncludes().AsNoTracking();
        }
        else if (findOptions.IsIgnoreAutoIncludes)
        {
            entity.IgnoreAutoIncludes();
        }
        else if (findOptions.IsAsNoTracking)
        {
            entity.AsNoTracking();
        }
        return entity;
    }
}
