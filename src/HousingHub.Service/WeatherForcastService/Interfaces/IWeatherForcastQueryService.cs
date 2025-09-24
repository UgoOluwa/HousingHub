using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos;

namespace HousingHub.Service.WeatherForcastService.Interfaces;

public interface IWeatherForcastQueryService
{
    Task<BaseResponse<WeatherForcastDto?>> GetWeatherForcastAsync(Guid id);
    Task<BaseResponse<List<WeatherForcastDto>>> GetAllWeatherForcastsAsync();
}
