using AutoMapper;
using HousingHub.Application.WeatherForecast.Commands.Create;
using HousingHub.Service.Dtos;

namespace HousingHub.Application.Commons.Mappings;

public class WeatherForcastMapper : Profile
{
    public WeatherForcastMapper()
    {
        CreateMap<CreateWeatherForecastCommand, CreateWeatherForecastDto>().ReverseMap();

        //CreateMap<CreateWeatherForecastCommand, WeatherForcastDto>().ReverseMap();
    }
}
