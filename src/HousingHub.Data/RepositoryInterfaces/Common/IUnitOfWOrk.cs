using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Data.RepositoryInterfaces.Queries;

namespace HousingHub.Data.RepositoryInterfaces.Common;

public interface IUnitOfWOrk : IDisposable
{
    #region Weather Forcast Repository
    IWeatherForcastCommadRepository WeatherForcastCommadRepository { get; }
    IWeatherForcastQueryRepository WeatherForcastQueryRepository { get; }
    #endregion

    Task SaveAsync();
}
