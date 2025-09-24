using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class WeatherForcastCommandRepository : GenericCommandRepository<WeatherForecast>, IWeatherForcastCommadRepository
{
    public WeatherForcastCommandRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
