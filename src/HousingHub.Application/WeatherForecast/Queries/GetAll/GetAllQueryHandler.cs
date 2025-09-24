using MediatR;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos;
using HousingHub.Service.WeatherForcastService.Interfaces;

namespace HousingHub.Application.WeatherForecast.Queries.GetAll;

public class GetAllQueryHandler : IRequestHandler<GetAllQuery, BaseResponse<List<WeatherForcastDto>>>
{
    public readonly IWeatherForcastQueryService _weatherForcastQueryService;

    public GetAllQueryHandler(IWeatherForcastQueryService weatherForcastQueryService)
    {
        _weatherForcastQueryService = weatherForcastQueryService;
    }

    public async Task<BaseResponse<List<WeatherForcastDto>>> Handle(GetAllQuery query, CancellationToken cancellationToken)
    {
        var response = await _weatherForcastQueryService.GetAllWeatherForcastsAsync();
        return new BaseResponse<List<WeatherForcastDto>>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
