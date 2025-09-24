using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using MediatR;
using HousingHub.Service.Dtos;
using HousingHub.Application.WeatherForecast.Queries.GetAll;
using HousingHub.Application.Commons.Bases;
using HousingHub.Application.WeatherForecast.Queries.GetById;
using HousingHub.Application.WeatherForecast.Commands.Create;

namespace HousingHub.API.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[Controller]")]

    public class WeatherForecastController : ControllerBase
    {
        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IMediator _mediator;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IMediator mediator)
        {
            _logger = logger;
            _mediator = mediator;
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            BaseResponse<List<WeatherForcastDto>> response = await _mediator.Send(new GetAllQuery());

            return Ok(response);
        }

        [HttpGet("Get/Id")]
        [ProducesResponseType(typeof(WeatherForcastDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromQuery] Guid id)
        {
            var response = await _mediator.Send(new GetByIdQuery(id));
            return Ok(response);
        }

        [HttpGet("GetTeam")]
        public IActionResult GetV1()
        {
            return Ok("V1 Get to be implemented");
        }

        [HttpPost(Name = "Post")]
        public async Task<IActionResult> Post(CreateWeatherForecastCommand command)
        {
            var response = await _mediator.Send(command);

            return Ok(response);
        }
    }
}
