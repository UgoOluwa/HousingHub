using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using MediatR;

namespace HousingHub.API.Controllers.V2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[Controller]")]

    public class CustomerController : ControllerBase
    {
        private readonly ILogger<CustomerController> _logger;
        private readonly IMediator _mediator;

        public CustomerController(ILogger<CustomerController> logger, IMediator mediator)
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
