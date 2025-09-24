using MediatR;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos;

namespace HousingHub.Application.WeatherForecast.Queries.GetAll;

public record GetAllQuery() : IRequest<BaseResponse<List<WeatherForcastDto>>>;