using Asp.Versioning;
using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Tenancy;
using HousingHub.Service.TenancyService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>
/// Tenancies, for staff.
/// </summary>
/// <remarks>
/// <para>
/// Read-only, with no exception. A tenancy is an agreement between two people, and
/// an endpoint that let an admin accept a document, change a fee or advance the
/// status would be a way to manufacture an executed agreement neither party made —
/// indistinguishable afterwards from the real thing. Where a payment has a refund
/// that is verifiable against the provider, a tenancy has no equivalent: nothing
/// outside this system could contradict a forged acceptance.
/// </para>
/// <para>
/// So what staff get is sight. Who is party to it, what was asked for, what was
/// signed and when, and which side the thing is currently stuck on. Putting it
/// right means talking to the parties, who are the only people who can.
/// </para>
/// <para>
/// No <c>[Authorize]</c> attribute: the API's FallbackPolicy already requires an
/// authenticated caller holding <c>role=Admin</c>, so every action here is closed by
/// default — the same posture as the other admin controllers.
/// </para>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminTenancyController : ControllerBase
{
    private readonly IAdminTenancyQueryService _tenancies;

    public AdminTenancyController(IAdminTenancyQueryService tenancies)
    {
        _tenancies = tenancies;
    }

    /// <summary>Tenancies, newest first.</summary>
    /// <param name="status">
    /// Optional filter. The useful ones operationally are DocumentsRequested — where
    /// something is waiting on somebody — and AwaitingPayment.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<AdminTenancyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TenancyStatus? status = null)
    {
        return Ok(await _tenancies.GetTenanciesAsync(pageNumber, pageSize, status));
    }

    /// <summary>One tenancy: the parties, the money, every document and its signature trail.</summary>
    [HttpGet("{tenancyId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<AdminTenancyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid tenancyId)
    {
        return Ok(await _tenancies.GetAsync(tenancyId));
    }

    /// <summary>
    /// A short-lived link to one document's file. SuperAdmin only.
    /// </summary>
    /// <remarks>
    /// Restricted beyond the general admin role because of what these files are: a
    /// tenant's employment letter, their references, their signed agreement. The
    /// metadata and the signature trail — which answer almost every question staff
    /// actually have — are on the detail endpoint and need no such access. Opening
    /// the file itself is for a dispute, and each one is logged.
    /// </remarks>
    [HttpGet("{tenancyId:guid}/documents/{documentId:guid}/url")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocumentUrl(
        Guid tenancyId, Guid documentId, [FromQuery] bool submitted = false)
    {
        return Ok(await _tenancies.GetDocumentUrlAsync(tenancyId, documentId, submitted));
    }
}
