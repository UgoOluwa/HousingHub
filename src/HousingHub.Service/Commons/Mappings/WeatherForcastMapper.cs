using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos;

namespace HousingHub.Service.Commons.Mappings;

public class WeatherForcastMapper : Profile
{
    public WeatherForcastMapper()
    {
        CreateMap<CreateWeatherForecastDto, WeatherForecast>();
        CreateMap<WeatherForecast, WeatherForcastDto>().ReverseMap();
    }
}
