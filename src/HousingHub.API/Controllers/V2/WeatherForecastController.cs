using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using MediatR;

namespace HousingHub.API.Controllers.V2
{
    [ApiController]
    [ApiVersion("2.0")]
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

        
        [HttpGet("GetTeam")]
        public IActionResult GetV2()
        {
            return Ok("V2 Get to be implemented");
        }
    }
}
