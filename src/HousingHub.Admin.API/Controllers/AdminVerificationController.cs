using System.Security.Claims;
using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Verification;
using HousingHub.Service.VerificationService.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.Admin.API.Controllers;

/// <summary>
/// The verification review queue.
/// </summary>
/// <remarks>
/// <para>
/// No <c>[Authorize]</c> attribute is needed: the API's FallbackPolicy already
/// requires an authenticated caller holding <c>role=Admin</c>, so every action here
/// is closed by default. That is the same posture as the other admin controllers.
/// </para>
/// <para>
/// Not restricted to SuperAdmin. Reviewing documents is the day-to-day work this
/// queue exists for, and confining it to one person makes the queue a bottleneck.
/// It does mean any admin can read a submitted title deed — worth revisiting when
/// there is a larger team, and noted in the readiness document as an open decision.
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminVerificationController : ControllerBase
{
    private readonly IVerificationService _verification;

    public AdminVerificationController(IVerificationService verification)
    {
        _verification = verification;
    }

    /// <summary>
    /// Cases awaiting a decision, oldest first.
    /// </summary>
    /// <remarks>
    /// Oldest first because this is a queue — newest-first quietly starves whoever
    /// has been waiting longest, who is also the applicant most likely to give up.
    /// Reads a sparse index containing only outstanding work, so it does not slow
    /// down as decided cases accumulate.
    /// </remarks>
    /// <param name="subjectType">Optional filter: Business or Property.</param>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<VerificationCaseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] VerificationSubjectType? subjectType = null)
    {
        return Ok(await _verification.GetReviewQueueAsync(pageNumber, pageSize, subjectType));
    }

    /// <summary>One case with every document and the submitter's details.</summary>
    [HttpGet("cases/{caseId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<VerificationCaseDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCase(Guid caseId)
    {
        return Ok(await _verification.GetCaseForReviewAsync(caseId));
    }

    /// <summary>
    /// A short-lived link to view a submitted document.
    /// </summary>
    /// <remarks>
    /// Ten minutes, and it is a bearer credential — anyone holding the link can read
    /// the document until it expires, so it must not be pasted into chat or a
    /// ticket.
    /// </remarks>
    [HttpGet("documents/{documentId:guid}/url")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocumentUrl(Guid documentId)
    {
        return Ok(await _verification.GetDocumentUrlForReviewAsync(documentId));
    }

    /// <summary>
    /// Claims a case, so two admins do not review the same submission at once.
    /// </summary>
    [HttpPut("cases/{caseId:guid}/begin-review")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BeginReview(Guid caseId)
    {
        var adminId = GetAuthenticatedAdminId();
        if (adminId is null) return Unauthorized();

        return Ok(await _verification.BeginReviewAsync(adminId.Value, caseId));
    }

    /// <summary>
    /// Approves or rejects one document.
    /// </summary>
    /// <remarks>
    /// Per document rather than per case, because the usual outcome is "five of these
    /// are fine and the sixth is illegible". Rejecting everything and making the
    /// applicant re-upload documents that were already good is how a verification
    /// flow earns a reputation for being painful.
    ///
    /// A rejection reason is required — it is what the applicant is shown, and a
    /// rejection they cannot act on just becomes a support ticket.
    /// </remarks>
    [HttpPut("documents/{documentId:guid}/review")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReviewDocument(Guid documentId, ReviewDocumentDto request)
    {
        var adminId = GetAuthenticatedAdminId();
        if (adminId is null) return Unauthorized();

        return Ok(await _verification.ReviewDocumentAsync(adminId.Value, documentId, request));
    }

    /// <summary>
    /// Records the final decision.
    /// </summary>
    /// <remarks>
    /// Approval requires every document to have been individually approved first.
    /// A badge resting on evidence nobody looked at is worse than no badge, because
    /// somebody will rely on it.
    ///
    /// Use <c>EscalatedNameMismatch</c> rather than a plain rejection when the name
    /// on the documents does not match the account holder. That is the strongest
    /// signal of attempted impersonation in this whole flow — a real company's real
    /// certificate submitted by someone with no connection to it — and it should be
    /// visible as its own outcome rather than buried among ordinary rejections.
    /// </remarks>
    [HttpPut("cases/{caseId:guid}/decide")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DecideCase(Guid caseId, DecideCaseDto request)
    {
        var adminId = GetAuthenticatedAdminId();
        if (adminId is null) return Unauthorized();

        return Ok(await _verification.DecideCaseAsync(adminId.Value, caseId, request));
    }

    private Guid? GetAuthenticatedAdminId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                 ?? User.FindFirst(ClaimTypes.NameIdentifier);

        if (claim != null && Guid.TryParse(claim.Value, out var adminId))
            return adminId;

        return null;
    }
}
