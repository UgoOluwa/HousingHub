using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

/// <summary>
/// Paying fees owed to Housing Hub.
/// </summary>
/// <remarks>
/// <para>
/// Every action derives the payer from the JWT. No endpoint accepts a customer id,
/// and — the part that matters most here — <b>no endpoint accepts an amount</b>.
/// Prices come from server configuration, so a client cannot choose what to pay.
/// </para>
/// <para>
/// The webhook lives on its own unversioned controller, because a provider's
/// configured URL should not have to change when the API version does.
/// </para>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentController(IPaymentService payments)
    {
        _payments = payments;
    }

    /// <summary>
    /// What a verification request costs, before paying for it.
    /// </summary>
    /// <remarks>
    /// Shows the identity check as its own line, so someone who has already been
    /// verified can see they are not being charged for it twice.
    /// </remarks>
    [HttpGet("verification-cases/{caseId:guid}/quote")]
    [ProducesResponseType(typeof(BaseResponse<PaymentQuoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QuoteVerificationCase(Guid caseId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _payments.QuoteVerificationCaseAsync(userId.Value, caseId));
    }

    /// <summary>
    /// Starts a payment for a verification request and returns where to send the payer.
    /// </summary>
    /// <remarks>
    /// Calling this twice in quick succession returns the same attempt rather than
    /// registering a second charge.
    /// </remarks>
    [HttpPost("verification-cases/{caseId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> InitialiseVerificationPayment(
        Guid caseId, [FromBody] InitialisePaymentRequest? request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _payments.InitialiseVerificationPaymentAsync(
            userId.Value, caseId, request?.CallbackUrl));
    }

    /// <summary>
    /// The current state of one of your payments.
    /// </summary>
    /// <remarks>
    /// What the page the payer is returned to should poll. Returning to a callback
    /// URL is not evidence of payment — only the provider's signed webhook settles
    /// one — so the client asks this until it reports Successful.
    /// </remarks>
    [HttpGet("{reference}")]
    [ProducesResponseType(typeof(BaseResponse<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByReference(string reference)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _payments.GetByReferenceAsync(userId.Value, reference));
    }

    /// <summary>Your payments, newest first.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(BaseResponse<List<PaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _payments.GetMyPaymentsAsync(userId.Value));
    }

    private Guid? GetAuthenticatedUserId()
    {
        var raw = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

/// <summary>
/// Where to return the payer after the gateway.
/// </summary>
/// <remarks>
/// Optional, and only ever a destination — never an amount, a price or a purpose.
/// </remarks>
public record InitialisePaymentRequest(string? CallbackUrl);
