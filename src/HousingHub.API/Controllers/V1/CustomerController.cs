using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Application.Commons.Bases;
using HousingHub.Application.Customer.Commands.Create;
using HousingHub.Application.Customer.Commands.Delete;
using HousingHub.Application.Customer.Commands.SubmitKyc;
using HousingHub.Application.Customer.Commands.UpdateProfile;
using HousingHub.Application.Customer.Commands.UploadKycDocument;
using HousingHub.Application.Customer.Commands.VerifyKyc;
using HousingHub.Application.Customer.Queries.GetAll;
using HousingHub.Application.Customer.Queries.GetById;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Customer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

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

        // ─── Admin: Create / Read / Delete ────────────────────────────────

        [HttpPost]
        [ProducesResponseType(typeof(BaseResponse<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create(CreateCustomerCommand command)
        {
            var response = await _mediator.Send(command);
            return Ok(response);
        }

        [Authorize]
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(BaseResponse<CustomerWithDetailsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(Guid id)
        {
            var response = await _mediator.Send(new GetCustomerByIdQuery(id));
            return Ok(response);
        }

        [Authorize]
        [HttpGet("all")]
        [ProducesResponseType(typeof(BaseResponsePagination<HousingHub.Core.CustomResponses.PaginatedResult<CustomerDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _mediator.Send(new GetAllCustomersQuery(pageNumber, pageSize));
            return Ok(response);
        }

        [Authorize]
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _mediator.Send(new DeleteCustomerCommand(id));
            return NoContent();
        }

        // ─── Profile ───────────────────────────────────────────────────────

        [Authorize]
        [HttpPut("profile")]
        [ProducesResponseType(typeof(BaseResponse<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateProfile(UpdateProfileCommand command)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            var enrichedCommand = command with { CustomerId = userId.Value };
            var response = await _mediator.Send(enrichedCommand);
            return Ok(response);
        }

        // ─── KYC ───────────────────────────────────────────────────────────

        [Authorize]
        [HttpPost("kyc")]
        [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SubmitKyc(SubmitKycCommand command)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            var enrichedCommand = command with { CustomerId = userId.Value };
            var response = await _mediator.Send(enrichedCommand);
            return Ok(response);
        }

        [Authorize]
        [HttpPost("kyc/document")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UploadKycDocument([FromForm] UploadKycDocumentRequest request)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null) return Unauthorized();

            var response = await _mediator.Send(new UploadKycDocumentCommand(userId.Value, request.File));
            return Ok(response);
        }

        [Authorize]
        [HttpPut("{id:guid}/kyc/verify")]
        [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> VerifyKyc(Guid id, [FromQuery] bool approve)
        {
            var response = await _mediator.Send(new VerifyKycCommand(id, approve));
            return Ok(response);
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private Guid? GetAuthenticatedUserId()
        {
            var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                     ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (claim != null && Guid.TryParse(claim.Value, out var userId))
                return userId;

            return null;
        }
    }

    public class UploadKycDocumentRequest
    {
        public IFormFile File { get; set; } = null!;
    }
}
