using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>
/// Payments, for staff.
/// </summary>
/// <remarks>
/// <para>
/// Read-only except for refunds, and that is a deliberate limit. An endpoint that
/// let an admin mark a payment successful, unflag one, or edit an amount would each
/// be a way to grant a paid service with no money moving, and the row it wrote would
/// be indistinguishable from the genuine thing. A refund is different: it is
/// verifiable against the provider afterwards, it moves money in the direction that
/// cannot enrich us, and the manual alternative leaves our own record still claiming
/// the customer paid.
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
    private readonly IAdminPaymentCommandService _refunds;

    public AdminPaymentController(
        IAdminPaymentQueryService payments,
        IAdminPaymentCommandService refunds)
    {
        _payments = payments;
        _refunds = refunds;
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

    /// <summary>
    /// Sends a payment back. SuperAdmin only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only action here that moves money, so it is the only one restricted
    /// beyond the general admin role. Every other endpoint on this controller reads.
    /// </para>
    /// <para>
    /// The amount refunded is what the provider says actually arrived, not what was
    /// asked for — those differ precisely when a payment was flagged, which is the
    /// commonest reason to refund one. A reason is required and is recorded against
    /// the payment alongside the admin who asked.
    /// </para>
    /// </remarks>
    [HttpPost("{reference}/refund")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(BaseResponse<AdminPaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refund(string reference, RefundPaymentRequest request)
    {
        var result = await _refunds.RefundAsync(reference, request.Reason ?? string.Empty, GetAdminId());

        if (!result.IsSuccessful) return BadRequest(result);
        return Ok(result);
    }

    private Guid GetAdminId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                 ?? User.FindFirst(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}

/// <summary>
/// Why money is being sent back.
/// </summary>
/// <remarks>
/// Note what is absent: an amount. The figure refunded comes from the provider, so
/// an admin cannot choose it — a partial refund goes through the provider's own
/// dashboard, where it is recorded against the transaction.
/// </remarks>
public record RefundPaymentRequest(string? Reason);
