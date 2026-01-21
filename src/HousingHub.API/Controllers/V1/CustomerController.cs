using Asp.Versioning;
using HousingHub.Application.Commons.Bases;
using HousingHub.Application.Customer.Commands.Create;
using HousingHub.Application.Customer.Commands.Delete;
using HousingHub.Application.Customer.Queries.GetAll;
using HousingHub.Application.Customer.Queries.GetById;
using HousingHub.Service.Dtos.Customer;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.API.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
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

        [HttpPost]
        [ProducesResponseType(typeof(BaseResponse<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create(CreateCustomerCommand command)
        {
            BaseResponse<CustomerDto?> response = await _mediator.Send(command);

            return Ok(response);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BaseResponse<CustomerWithDetailsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(Guid id)
        {
            var query = new GetCustomerByIdQuery(id);
            var response = await _mediator.Send(query);
            return Ok(response);
        }

        [HttpGet("all")]
        [ProducesResponseType(typeof(BaseResponse<List<CustomerDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()

        {
            var response = await _mediator.Send(new GetAllCustomersQuery());
            return Ok(response);
        }
        

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var command = new DeleteCustomerCommand(id);
            await _mediator.Send(command);
            return NoContent();
        }


    }
}
