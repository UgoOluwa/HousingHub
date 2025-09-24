using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class WeatherForcastQueryRepository : GenericQueryRepository<WeatherForecast>, IWeatherForcastQueryRepository
{
    public WeatherForcastQueryRepository(AppDbContext dbContext) 
        : base(dbContext)
    {
        
    }
}
