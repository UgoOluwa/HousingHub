using Microsoft.Extensions.Logging;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos;
using HousingHub.Service.WeatherForcastService.Interfaces;

namespace HousingHub.Service.WeatherForcastService;

public class WeatherForcastCommandService : IWeatherForcastCommandService
{
    public readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<WeatherForcastCommandService> _logger;
    private const string ClassName = "weatherForcast";

    public WeatherForcastCommandService(ILogger<WeatherForcastCommandService> logger, IUnitOfWOrk unitOfWOrk)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
    }

    public async Task<BaseResponse<CreateWeatherForcastResponseDto>> CreateWeatherForcast(CreateWeatherForecastDto request)
    {
        try
        {
            var newEntity = new WeatherForecast(request.Date, request.TemperatureC, request.Summary);
            var isSuccessful = await _unitOfWOrk.WeatherForcastCommadRepository.InsertAsync(newEntity);
            if (!isSuccessful) {
                return new BaseResponse<CreateWeatherForcastResponseDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));
            }

            await _unitOfWOrk.SaveAsync();
            return new BaseResponse<CreateWeatherForcastResponseDto>(new CreateWeatherForcastResponseDto(newEntity.Id), true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex) {
            _logger.LogError(ex, ex.Message);
            return new BaseResponse<CreateWeatherForcastResponseDto>(null, false, string.Empty, ex.Message);
        }
        
    }
}
