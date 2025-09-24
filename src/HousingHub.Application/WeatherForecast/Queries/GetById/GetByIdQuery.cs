using MediatR;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos;

namespace HousingHub.Application.WeatherForecast.Queries.GetById;

public record GetByIdQuery(Guid Id) : IRequest<BaseResponse<WeatherForcastDto>>;
