using System.Linq.Expressions;
using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class GenericCommandRepository<T> : IGenericCommandRepository<T> where T : class
{
    protected readonly IDynamoDBContext _context;

    public GenericCommandRepository(IDynamoDBContext context)
    {
        _context = context;
    }

    public async Task<bool> InsertAsync(T entity)
    {
        SetTimestamps(entity, isNew: true);
        await _context.SaveAsync(entity);
        return true;
    }

    public async Task UpdateAsync(T entity)
    {
        SetTimestamps(entity, isNew: false);
        await _context.SaveAsync(entity);
    }

    public async Task DeleteAsync(T entity)
    {
        await _context.DeleteAsync(entity);
    }

    public async Task InsertRangeAsync(IEnumerable<T> entities)
    {
        var batch = _context.CreateBatchWrite<T>();
        foreach (var entity in entities)
        {
            SetTimestamps(entity, isNew: true);
            batch.AddPutItem(entity);
        }
        await batch.ExecuteAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<T> entities)
    {
        var batch = _context.CreateBatchWrite<T>();
        foreach (var entity in entities)
        {
            SetTimestamps(entity, isNew: false);
            batch.AddPutItem(entity);
        }
        await batch.ExecuteAsync();
    }

    public async Task DeleteRangeAsync(IEnumerable<T> entities)
    {
        var batch = _context.CreateBatchWrite<T>();
        foreach (var entity in entities)
        {
            batch.AddDeleteItem(entity);
        }
        await batch.ExecuteAsync();
    }

    public async Task<bool> ExistAsync(Expression<Func<T, bool>> predicate)
    {
        var items = await ScanAllAsync();
        return items.AsQueryable().Any(predicate);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        var items = await ScanAllAsync();
        return items.AsQueryable().Count(predicate);
    }

    public async Task<IEnumerable<T>> GetWhereAsync(Expression<Func<T, bool>> predicate)
    {
        var items = await ScanAllAsync();
        return items.AsQueryable().Where(predicate).ToList();
    }

    public async Task<T?> GetSingleAsync(Expression<Func<T, bool>> predicate)
    {
        var items = await ScanAllAsync();
        return items.AsQueryable().FirstOrDefault(predicate);
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _context.LoadAsync<T>(id);
    }

    public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null)
    {
        var items = await ScanAllAsync();
        return predicate == null ? items : items.AsQueryable().Where(predicate).ToList();
    }

    public async Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null)
    {
        var items = await ScanAllAsync();
        return predicate == null ? items : items.AsQueryable().Where(predicate).ToList();
    }

    private async Task<List<T>> ScanAllAsync()
    {
        var search = _context.ScanAsync<T>(new List<ScanCondition>());
        return await search.GetRemainingAsync();
    }

    private static void SetTimestamps(T entity, bool isNew)
    {
        if (entity is BaseEntity baseEntity)
        {
            var utcNow = DateTime.UtcNow;
            if (isNew)
            {
                baseEntity.DateCreated = utcNow;
                baseEntity.IsActive = true;
            }
            baseEntity.DateModified = utcNow;
        }
    }
}
