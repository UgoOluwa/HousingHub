using MediatR;
using HousingHub.Application.Commons.Bases;

namespace HousingHub.Application.WeatherForecast.Commands.Create;

public record CreateWeatherForecastCommand(DateOnly Date, int TemperatureC, string? Summary) : IRequest<BaseResponse<Guid?>>;