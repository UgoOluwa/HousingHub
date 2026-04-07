using System.Linq.Expressions;
using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;

namespace HousingHub.Repository.Queries;

public partial class GenericQueryRepository<T> : IGenericQueryRepository<T> where T : class
{
    protected readonly IDynamoDBContext _context;

    public GenericQueryRepository(IDynamoDBContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByAsync(Expression<Func<T, bool>> predicate)
    {
        var items = await ScanAllAsync();
        return items.AsQueryable().FirstOrDefault(predicate);
    }

    public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> predicate)
    {
        var items = await ScanAllAsync();
        return items.AsQueryable().Where(predicate).ToList();
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await ScanAllAsync();
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        var items = await ScanAllAsync();
        return items.AsQueryable().Any(predicate);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        var items = await ScanAllAsync();
        return items.AsQueryable().Count(predicate);
    }

    public async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<T, bool>>? predicate = null)
    {
        var items = await ScanAllAsync();
        IQueryable<T> query = items.AsQueryable();

        if (predicate != null)
            query = query.Where(predicate);

        var totalCount = query.Count();
        var pagedItems = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (pagedItems, totalCount);
    }

    private async Task<List<T>> ScanAllAsync()
    {
        var search = _context.ScanAsync<T>(new List<ScanCondition>());
        return await search.GetRemainingAsync();
    }
}
