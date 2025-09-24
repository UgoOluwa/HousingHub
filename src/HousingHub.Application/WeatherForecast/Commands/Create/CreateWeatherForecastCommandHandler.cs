using AutoMapper;
using MediatR;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos;
using HousingHub.Service.WeatherForcastService.Interfaces;

namespace HousingHub.Application.WeatherForecast.Commands.Create;

public class CreateWeatherForecastCommandHandler : IRequestHandler<CreateWeatherForecastCommand, BaseResponse<Guid?>>
{
    public readonly IWeatherForcastCommandService _commandService;
    public readonly IMapper _mapper;
    public CreateWeatherForecastCommandHandler(IWeatherForcastCommandService commandService, IMapper mapper)
    {
        _commandService = commandService;
        _mapper = mapper;
    }
    public async Task<BaseResponse<Guid?>> Handle(CreateWeatherForecastCommand request, CancellationToken cancellationToken)
    {
        var response = await _commandService.CreateWeatherForcast(_mapper.Map<CreateWeatherForecastDto>(request));
        
        return new BaseResponse<Guid?>(response.IsSuccessful, response?.Data?.Id, response?.Message, null);
    }
}
