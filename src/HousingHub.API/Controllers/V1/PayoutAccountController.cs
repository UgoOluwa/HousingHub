using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PayoutService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

/// <summary>
/// Where an owner's rent is paid out to.
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint acts on the caller's own payout account and takes no customer id
/// — there is no route by which one person can read or set another's. Money is
/// addressed by what these create, so a missing ownership check here is somebody
/// else's rent.
/// </para>
/// <para>
/// Gated by <c>PropertyOwnerOrAgent</c>: a renter has no property to be paid for.
/// </para>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "PropertyOwnerOrAgent")]
public class PayoutAccountController : ControllerBase
{
    private readonly IPayoutAccountService _payouts;

    public PayoutAccountController(IPayoutAccountService payouts)
    {
        _payouts = payouts;
    }

    /// <summary>Banks a payout account can be held with.</summary>
    [HttpGet("banks")]
    [ProducesResponseType(typeof(BaseResponse<List<BankDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBanks() => Ok(await _payouts.GetBanksAsync());

    /// <summary>The caller's own payout account, or null if they have not set one.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(BaseResponse<PayoutAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _payouts.GetMineAsync(userId.Value));
    }

    /// <summary>
    /// Asks the bank whose account this is, creating nothing.
    /// </summary>
    /// <remarks>
    /// So the owner sees whose account they have typed before committing to it. The
    /// commonest cause of a refused match is a mistyped digit, and discovering that
    /// as a rejection is worse than seeing the wrong name.
    /// </remarks>
    [HttpPost("resolve")]
    [ProducesResponseType(typeof(BaseResponse<ResolvedAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(SavePayoutAccountDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _payouts.ResolveAsync(request, userId.Value));
    }

    /// <summary>
    /// Makes this account the caller's payout destination.
    /// </summary>
    /// <remarks>
    /// Refused unless the bank's own name for the account matches the identity an
    /// admin verified. Replaces any existing account rather than adding a second.
    /// </remarks>
    [HttpPut]
    [ProducesResponseType(typeof(BaseResponse<PayoutAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Save(SavePayoutAccountDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _payouts.SaveAsync(request, userId.Value));
    }

    private Guid? GetAuthenticatedUserId()
    {
        var raw = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
