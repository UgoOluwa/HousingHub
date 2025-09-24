using MediatR;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos;
using HousingHub.Service.WeatherForcastService.Interfaces;

namespace HousingHub.Application.WeatherForecast.Queries.GetById;

public class GetByIdQueryHandler : IRequestHandler<GetByIdQuery, BaseResponse<WeatherForcastDto>>
{
    public readonly IWeatherForcastQueryService _weatherForcastQueryService;

    public GetByIdQueryHandler(IWeatherForcastQueryService weatherForcastQueryService)
    {
        _weatherForcastQueryService = weatherForcastQueryService;
    }

    public async Task<BaseResponse<WeatherForcastDto>> Handle(GetByIdQuery query, CancellationToken cancellationToken)
    {
        var response = await _weatherForcastQueryService.GetWeatherForcastAsync(query.Id);
        return new BaseResponse<WeatherForcastDto>(response.IsSuccessful, response.Data, response.Message, null);
    } }