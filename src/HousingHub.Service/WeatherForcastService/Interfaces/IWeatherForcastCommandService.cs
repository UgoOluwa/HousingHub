using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos;

namespace HousingHub.Service.WeatherForcastService.Interfaces;

public interface IWeatherForcastCommandService
{
    Task<BaseResponse<CreateWeatherForcastResponseDto>> CreateWeatherForcast(CreateWeatherForecastDto request);
}
