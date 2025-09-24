using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Data.RepositoryInterfaces.Queries;

namespace HousingHub.Repository.Commands;

public class UnitOfWork : IUnitOfWOrk
{
    public IWeatherForcastCommadRepository WeatherForcastCommadRepository { get; }
    public IWeatherForcastQueryRepository WeatherForcastQueryRepository { get; }
    private readonly AppDbContext _applicationContext;


    public UnitOfWork(IWeatherForcastCommadRepository weatherForcastCommadRepository, IWeatherForcastQueryRepository weatherForcastQueryRepository, AppDbContext applicationContext)
    {
        WeatherForcastCommadRepository = weatherForcastCommadRepository ?? throw new ArgumentNullException(nameof(weatherForcastCommadRepository)); ;
        WeatherForcastQueryRepository = weatherForcastQueryRepository ?? throw new ArgumentNullException(nameof(weatherForcastQueryRepository)); ;
        _applicationContext = applicationContext;
    }

    public async Task SaveAsync()
    {
        await _applicationContext.SaveChangesAsync();
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _applicationContext.Dispose();
        }
    }

}
