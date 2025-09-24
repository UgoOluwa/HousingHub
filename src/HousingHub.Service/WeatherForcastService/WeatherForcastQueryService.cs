using AutoMapper;
using Microsoft.Extensions.Logging;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Service.Dtos;
using HousingHub.Service.RepositoryInterfaces.Common;
using HousingHub.Service.WeatherForcastService.Interfaces;

namespace HousingHub.Service.WeatherForcastService;

public class WeatherForcastQueryService : IWeatherForcastQueryService
{
    public readonly IWeatherForcastQueryRepository _weatherForcastQueryRepository;
    public readonly IMapper _mapper;
    private readonly ILogger<WeatherForcastQueryService> _logger;
    private const string ClassName = "weatherforcast";

    public WeatherForcastQueryService(IWeatherForcastQueryRepository weatherForcastQueryRepository, IMapper mapper, ILogger<WeatherForcastQueryService> logger)
    {
        _weatherForcastQueryRepository = weatherForcastQueryRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<WeatherForcastDto?>> GetWeatherForcastAsync(Guid id)
    {
        try
        {
            var forCast = await _weatherForcastQueryRepository.GetByAsync(x => x.Id == id, new FindOptions() { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });
            if (forCast is null) {
                return new BaseResponse<WeatherForcastDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<WeatherForcastDto?>(_mapper.Map<WeatherForcastDto>(forCast), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex) {
            _logger.LogError(ex.ToString());
            return new BaseResponse<WeatherForcastDto?>(null, false, string.Empty, ex.Message);
        }       
    }

    public async Task<BaseResponse<List<WeatherForcastDto>>> GetAllWeatherForcastsAsync()
    {
        try
        {
            var forcasts = await _weatherForcastQueryRepository.GetAllAsync(new FindOptions() { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });
            if (!forcasts.Any())
            {
                return new BaseResponse<List<WeatherForcastDto>>(new List<WeatherForcastDto>() , false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<List<WeatherForcastDto>>(_mapper.Map<List<WeatherForcastDto>>(forcasts), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex) { 
            _logger.LogError(ex.ToString());
            return new BaseResponse<List<WeatherForcastDto>>([], false, string.Empty, ex.Message);
        }
    }
}
