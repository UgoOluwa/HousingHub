using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Tenancy;
using HousingHub.Service.TenancyService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

/// <summary>
/// Choosing a tenant for a property, and backing out of having chosen one.
/// </summary>
/// <remarks>
/// <para>
/// The step between an inspection and an agreement. Every action here derives the
/// caller from the JWT; no endpoint accepts a customer id for the caller, and the
/// service re-checks which side of the tenancy they are on.
/// </para>
/// <para>
/// Selecting is owner-only and gated by the <c>PropertyOwnerOrAgent</c> policy.
/// Declining is the tenant's, and is deliberately open to any authenticated user —
/// the service establishes that they are the tenant on that specific tenancy, which
/// is a stronger check than a role.
/// </para>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class TenancyController : ControllerBase
{
    private readonly ITenancyService _tenancies;

    public TenancyController(ITenancyService tenancies)
    {
        _tenancies = tenancies;
    }

    /// <summary>
    /// Everyone who completed an inspection for this property and could be chosen.
    /// </summary>
    /// <remarks>
    /// Completed inspections only — somebody who booked and never attended has not
    /// seen the property. Carries no contact details; messaging happens in-app.
    /// </remarks>
    [Authorize(Policy = "PropertyOwnerOrAgent")]
    [HttpGet("properties/{propertyId:guid}/candidates")]
    [ProducesResponseType(typeof(BaseResponse<List<TenancyCandidateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCandidates(Guid propertyId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _tenancies.GetCandidatesAsync(propertyId, userId.Value));
    }

    /// <summary>
    /// Choose somebody for this property.
    /// </summary>
    /// <remarks>
    /// Creates the tenancy everything afterwards hangs off, marks the listing under
    /// offer, and tells the candidate in-app and by email. Refused if the property
    /// already has a live tenancy — withdraw that first.
    /// </remarks>
    [Authorize(Policy = "PropertyOwnerOrAgent")]
    [HttpPost("properties/{propertyId:guid}/select")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SelectCandidate(Guid propertyId, SelectCandidateDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _tenancies.SelectCandidateAsync(propertyId, request.CustomerId, userId.Value));
    }

    /// <summary>
    /// Pull out of a selection. Owner only.
    /// </summary>
    /// <remarks>
    /// Releases the listing back to available and tells the candidate, with the
    /// reason if one was given.
    /// </remarks>
    [HttpPut("{tenancyId:guid}/withdraw")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Withdraw(Guid tenancyId, WithdrawTenancyDto? request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _tenancies.WithdrawAsync(tenancyId, request?.Reason, userId.Value));
    }

    /// <summary>
    /// Say you are no longer interested. The chosen candidate only.
    /// </summary>
    /// <remarks>
    /// Exists so a selection cannot get stuck: without it, an owner whose candidate
    /// has moved on waits indefinitely while the property stays under offer.
    /// </remarks>
    [HttpPut("{tenancyId:guid}/decline")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Decline(Guid tenancyId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _tenancies.DeclineAsync(tenancyId, userId.Value));
    }

    /// <summary>Everything you are party to, as owner or as tenant, newest first.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(BaseResponse<List<TenancyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _tenancies.GetMyTenanciesAsync(userId.Value));
    }

    /// <summary>One tenancy. Readable by its two parties and nobody else.</summary>
    [HttpGet("{tenancyId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid tenancyId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _tenancies.GetAsync(tenancyId, userId.Value));
    }

    private Guid? GetAuthenticatedUserId()
    {
        var raw = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
