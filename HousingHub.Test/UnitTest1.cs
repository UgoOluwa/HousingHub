// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using HousingHub.Application.Commons.Bases;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Service.Dtos;
using HousingHub.Service.RepositoryInterfaces.Common;
using HousingHub.Service.WeatherForcastService;
using HousingHub.Service.WeatherForcastService.Interfaces;

namespace HousingHub.Test;

public class UnitTest1
{
    public readonly IWeatherForcastQueryService _weatherForcastQueryService;
    public readonly Mock<IMapper> _mapper = new Mock<IMapper>();
    private readonly Mock<ILogger<WeatherForcastQueryService>> _logger = new Mock<ILogger<WeatherForcastQueryService>>();
    public readonly Mock<IWeatherForcastQueryRepository> _weatherForcastQueryRepository = new Mock<IWeatherForcastQueryRepository>();

    private const string ClassName = "weatherforcast";

    public UnitTest1()
    {
        _weatherForcastQueryService = new WeatherForcastQueryService(_weatherForcastQueryRepository.Object, _mapper.Object, _logger.Object);
    }

    [Fact]
    public async void Test1()
    {
        _weatherForcastQueryRepository.Setup(x => x.GetByAsync(It.IsAny<>(), It.IsAny<FindOptions>()))
            .Returns(new BaseResponse<WeatherForcastDto?>());

        var actual = await _weatherForcastQueryService.GetWeatherForcastAsync();
    }
}
