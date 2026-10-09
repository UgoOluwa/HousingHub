using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Core.CustomResponses;
using HousingHub.Core.Security;
using HousingHub.Model.Enums;
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
    private readonly ITenancyDocumentService _documents;

    public TenancyController(ITenancyService tenancies, ITenancyDocumentService documents)
    {
        _tenancies = tenancies;
        _documents = documents;
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

    // ─── Documents and fees ──────────────────────────────────────

    /// <summary>
    /// Everything asked for, every fee, and the total. Either party.
    /// </summary>
    /// <remarks>
    /// One response on purpose: the fees exist to be seen before anything is signed,
    /// and a second call is how a client ends up rendering the documents without them.
    /// </remarks>
    [HttpGet("{tenancyId:guid}/documents")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDocumentPackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocuments(Guid tenancyId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _documents.GetPackAsync(tenancyId, userId.Value));
    }

    /// <summary>
    /// Adds a document to a request still being composed. Owner only.
    /// </summary>
    /// <remarks>
    /// Multipart, because a document the tenant has to sign arrives with the file
    /// attached. A document the tenant supplies themselves carries no file, and
    /// attaching one is refused rather than ignored.
    /// </remarks>
    [HttpPost("{tenancyId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDocument(Guid tenancyId, [FromForm] AddTenancyDocumentDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _documents.AddDocumentAsync(tenancyId, request, userId.Value));
    }

    /// <summary>Removes one, while the request is still being composed. Owner only.</summary>
    [HttpDelete("{tenancyId:guid}/documents/{documentId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveDocument(Guid tenancyId, Guid documentId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _documents.RemoveDocumentAsync(tenancyId, documentId, userId.Value));
    }

    /// <summary>Replaces the fee list wholesale. Owner only, while composing.</summary>
    [HttpPut("{tenancyId:guid}/fees")]
    [ProducesResponseType(typeof(BaseResponse<IReadOnlyList<TenancyFeeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetFees(Guid tenancyId, SetTenancyFeesDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _documents.SetFeesAsync(tenancyId, request, userId.Value));
    }

    /// <summary>
    /// Sends the whole request to the tenant. Owner only, and only once.
    /// </summary>
    /// <remarks>
    /// Refused without a tenancy agreement in the set — every let has one. Emails the
    /// tenant with the total so no figure appears for the first time at payment.
    /// </remarks>
    [HttpPost("{tenancyId:guid}/documents/send")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDocumentPackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendDocuments(Guid tenancyId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _documents.SendRequestAsync(tenancyId, userId.Value));
    }

    /// <summary>The tenant returns a file — their own document, or a signed scan.</summary>
    [HttpPost("{tenancyId:guid}/documents/{documentId:guid}/submit")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitDocument(Guid tenancyId, Guid documentId, IFormFile file)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _documents.SubmitDocumentAsync(tenancyId, documentId, file, userId.Value));
    }

    /// <summary>
    /// The tenant signs in the app.
    /// </summary>
    /// <remarks>
    /// The address and user agent are taken from the request rather than the body —
    /// they are the audit trail, and a signer supplying their own would be attesting
    /// to whatever they liked. Refused for anything Nigerian law will not let be
    /// signed electronically.
    /// </remarks>
    [HttpPost("{tenancyId:guid}/documents/{documentId:guid}/sign")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SignDocument(Guid tenancyId, Guid documentId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _documents.SignDocumentAsync(
            tenancyId,
            documentId,
            userId.Value,
            ClientAddressResolver.Resolve(
                Request.Headers["X-Forwarded-For"].FirstOrDefault(),
                HttpContext.Connection.RemoteIpAddress?.ToString()),
            Request.Headers.UserAgent.FirstOrDefault()));
    }

    /// <summary>The owner accepts a document or sends it back with a reason.</summary>
    [HttpPut("{tenancyId:guid}/documents/{documentId:guid}/review")]
    [ProducesResponseType(typeof(BaseResponse<TenancyDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReviewDocument(
        Guid tenancyId, Guid documentId, ReviewTenancyDocumentDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _documents.ReviewDocumentAsync(tenancyId, documentId, request, userId.Value));
    }

    /// <summary>
    /// A short-lived link to one of a document's files.
    /// </summary>
    /// <remarks>
    /// Treat the URL as a credential rather than an address: anyone holding it can
    /// read the document until it expires. Fetch it on click and discard it.
    /// </remarks>
    /// <param name="file">
    /// Source — what the owner supplied. Submitted — what the tenant sent back.
    /// Signed — the stamped PDF made when the tenant signed in the app.
    /// </param>
    [HttpGet("{tenancyId:guid}/documents/{documentId:guid}/url")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocumentUrl(
        Guid tenancyId,
        Guid documentId,
        [FromQuery] TenancyDocumentFile file = TenancyDocumentFile.Source)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _documents.GetDocumentUrlAsync(tenancyId, documentId, file, userId.Value));
    }

    private Guid? GetAuthenticatedUserId()
    {
        var raw = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
