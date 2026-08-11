using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Verification;
using HousingHub.Service.VerificationService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

/// <summary>
/// Submitting documents for business or property verification.
/// </summary>
/// <remarks>
/// The whole controller requires authentication, and every action derives the
/// caller from the JWT rather than the request. No endpoint here accepts a
/// customer id — the only identifier a client supplies is a case or document id,
/// and the service re-checks ownership of both.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class VerificationController : ControllerBase
{
    private readonly IVerificationService _verification;

    public VerificationController(IVerificationService verification)
    {
        _verification = verification;
    }

    /// <summary>
    /// Opens a verification request, or returns the draft already in progress.
    /// </summary>
    /// <remarks>
    /// For a business request the subject is always you. For a property request,
    /// supply the listing id — it must be one you own.
    /// </remarks>
    [HttpPost("cases")]
    [ProducesResponseType(typeof(BaseResponse<VerificationCaseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartCase(StartVerificationCaseDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _verification.StartCaseAsync(userId.Value, request));
    }

    /// <summary>Every verification request you have made, newest first.</summary>
    [HttpGet("cases")]
    [ProducesResponseType(typeof(BaseResponse<List<VerificationCaseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCases()
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _verification.GetMyCasesAsync(userId.Value));
    }

    /// <summary>One request, with its documents and anything still outstanding.</summary>
    [HttpGet("cases/{caseId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<VerificationCaseDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCase(Guid caseId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _verification.GetMyCaseAsync(userId.Value, caseId));
    }

    /// <summary>
    /// Attaches a document to a draft request.
    /// </summary>
    /// <remarks>
    /// Accepts JPEG, PNG, WebP and PDF up to 15MB — scans of certificates run large.
    /// The file's actual bytes are checked against its extension, so renaming
    /// something to .pdf will not get it accepted.
    /// </remarks>
    [HttpPost("cases/{caseId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BaseResponse<VerificationDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<VerificationDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddDocument(
        Guid caseId, [FromForm] AddVerificationDocumentRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        // Same defensive binding as the KYC and property-photo uploads: binding a
        // single IFormFile through a wrapper is fragile, so accept the raw form file
        // too rather than handing a null to the service and surfacing it as a 500.
        var file = request.File
                   ?? Request.Form.Files.GetFile("File")
                   ?? Request.Form.Files.FirstOrDefault();

        if (file is null || file.Length == 0)
        {
            return BadRequest(new BaseResponse<VerificationDocumentDto>(
                null, false, string.Empty, ResponseMessages.NoFileProvided));
        }

        var metadata = new AddVerificationDocumentDto(
            request.DocumentType,
            request.DocumentNumber,
            request.NameOnDocument,
            request.IssuingAuthority,
            request.IssuedAt,
            request.ExpiresAt);

        return Ok(await _verification.AddDocumentAsync(userId.Value, caseId, metadata, file));
    }

    /// <summary>Removes a document from a draft request.</summary>
    [HttpDelete("cases/{caseId:guid}/documents/{documentId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveDocument(Guid caseId, Guid documentId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _verification.RemoveDocumentAsync(userId.Value, caseId, documentId));
    }

    /// <summary>
    /// A short-lived link to one of your own uploaded documents.
    /// </summary>
    /// <remarks>
    /// Ten minutes. The link carries its own signature, so treat it as a credential
    /// rather than an address — anyone holding it can read the document until it
    /// expires.
    /// </remarks>
    [HttpGet("cases/{caseId:guid}/documents/{documentId:guid}/url")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyDocumentUrl(Guid caseId, Guid documentId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _verification.GetMyDocumentUrlAsync(userId.Value, caseId, documentId));
    }

    /// <summary>
    /// Submits the request for review. Documents can no longer be changed afterwards.
    /// </summary>
    [HttpPut("cases/{caseId:guid}/submit")]
    [ProducesResponseType(typeof(BaseResponse<VerificationCaseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitCase(Guid caseId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _verification.SubmitCaseAsync(userId.Value, caseId));
    }

    private Guid? GetAuthenticatedUserId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                 ?? User.FindFirst(ClaimTypes.NameIdentifier);

        if (claim != null && Guid.TryParse(claim.Value, out var userId))
            return userId;

        return null;
    }
}

/// <summary>
/// Multipart form for a document upload.
/// </summary>
/// <remarks>
/// Separate from <see cref="AddVerificationDocumentDto"/> because multipart binding
/// needs a class with an IFormFile property, and mixing that into the service-layer
/// DTO would drag ASP.NET Core's form types into the service contract.
/// </remarks>
public class AddVerificationDocumentRequest
{
    public IFormFile? File { get; set; }
    public Model.Enums.VerificationDocumentType DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? NameOnDocument { get; set; }
    public string? IssuingAuthority { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
