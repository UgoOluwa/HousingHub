using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>
/// Payments, for staff.
/// </summary>
/// <remarks>
/// <para>
/// Read-only, and that is a deliberate limit rather than an unfinished one. An
/// endpoint that let an admin mark a payment successful would be a way to grant paid
/// services with no money moving, and the resulting row would be indistinguishable
/// from a genuine settlement. Refunds go through Paystack's dashboard, where they
/// are recorded against the transaction that actually exists.
/// </para>
/// <para>
/// No <c>[Authorize]</c> attribute: the API's FallbackPolicy already requires an
/// authenticated caller holding <c>role=Admin</c>, so every action here is closed
/// by default — the same posture as the other admin controllers.
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminPaymentController : ControllerBase
{
    private readonly IAdminPaymentQueryService _payments;

    public AdminPaymentController(IAdminPaymentQueryService payments)
    {
        _payments = payments;
    }

    /// <summary>Payments, newest first.</summary>
    /// <param name="status">Optional filter — Pending, Successful, Failed, Abandoned or Flagged.</param>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<AdminPaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] PaymentStatus? status = null)
    {
        return Ok(await _payments.GetPaymentsAsync(pageNumber, pageSize, status));
    }

    /// <summary>
    /// Payments where money may have moved and nothing was handed over.
    /// </summary>
    /// <remarks>
    /// The queue that matters. A flagged payment is one the gateway confirmed for an
    /// amount that does not match what was asked for — so it cannot be resolved by
    /// code, and until somebody looks, a customer has paid for something they did
    /// not get.
    /// </remarks>
    [HttpGet("flagged")]
    [ProducesResponseType(typeof(BaseResponse<List<AdminPaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFlagged()
    {
        return Ok(await _payments.GetFlaggedAsync());
    }

    /// <summary>How many payments are waiting on a person.</summary>
    [HttpGet("flagged/count")]
    [ProducesResponseType(typeof(BaseResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFlaggedCount()
    {
        return Ok(await _payments.GetFlaggedCountAsync());
    }

    /// <summary>One payment, by its Housing Hub reference.</summary>
    [HttpGet("{reference}")]
    [ProducesResponseType(typeof(BaseResponse<AdminPaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByReference(string reference)
    {
        return Ok(await _payments.GetByReferenceAsync(reference));
    }
}
