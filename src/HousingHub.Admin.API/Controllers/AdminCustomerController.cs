using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Customer;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>Manage regular customers (CustomerType = Customer).</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminCustomerController(
    ICustomerQueryService customerQueryService,
    ICustomerCommandService customerCommandService,
    IFileStorageService fileStorageService) : ControllerBase
{
    /// <summary>How long a KYC document viewing link stays valid.</summary>
    private static readonly TimeSpan KycDocumentLinkLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Returns a filtered, paginated list of customers.</summary>
    /// <remarks>
    /// Only returns accounts with CustomerType = Customer (excludes owners, agents, and admins).
    /// Supports search by name, email, or phone number; filter by KYC verification status and active status.
    /// </remarks>
    /// <param name="filter">Search and filter parameters.</param>
    /// <response code="200">Paginated list of customers.</response>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<AdminCustomerListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AdminCustomerFilterDto filter)
    {
        var result = await customerQueryService.GetCustomersFilteredAsync(filter, CustomerType.Customer);
        return Ok(result);
    }

    /// <summary>Returns full details of a single customer including KYC and personal info.</summary>
    /// <param name="id">Customer's database ID.</param>
    /// <response code="200">Customer details.</response>
    /// <response code="404">Customer not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<CustomerWithDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await customerQueryService.GetCustomerAsync(id);
        if (result.Data == null) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Approves or rejects a customer's KYC submission.</summary>
    /// <param name="id">Customer's database ID.</param>
    /// <param name="approve">True to approve, false to reject.</param>
    /// <param name="reason">Optional reason shown to the customer when rejecting.</param>
    /// <response code="200">KYC decision applied.</response>
    /// <response code="404">Customer not found.</response>
    [HttpPut("{id:guid}/kyc/verify")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyKyc(Guid id, [FromQuery] bool approve, [FromQuery] string? reason = null)
    {
        var result = await customerCommandService.VerifyKyc(id, approve, reason);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Mints a short-lived link for viewing a customer's KYC identity document.</summary>
    /// <remarks>
    /// KYC documents live in a private bucket prefix and are not publicly readable, so
    /// the stored value is an object key rather than a URL. This returns a presigned
    /// URL valid for a few minutes, which is long enough to review and short enough
    /// that a leaked link is not a lasting exposure.
    /// </remarks>
    /// <param name="id">Customer's database ID.</param>
    /// <response code="200">Presigned URL.</response>
    /// <response code="404">Customer not found, or no document on file.</response>
    [HttpGet("{id:guid}/kyc/document-url")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetKycDocumentUrl(Guid id)
    {
        var customer = await customerQueryService.GetCustomerAsync(id);

        var stored = customer?.Data?.IdDocumentUrl;
        if (string.IsNullOrWhiteSpace(stored))
            return NotFound(new BaseResponse<string?>(null, false, string.Empty, ResponseMessages.KycDocumentNotOnFile));

        // Documents submitted before KYC moved to the private bucket are stored as full
        // public URLs rather than object keys. Presigning those would produce nonsense,
        // so hand them back as-is and flag that the object is still publicly readable —
        // those rows need migrating into the private prefix.
        if (stored.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new BaseResponse<string>(
                stored, true, string.Empty,
                "Legacy document stored in the public bucket — pending migration."));
        }

        var url = await fileStorageService.GetPresignedUrlAsync(stored, KycDocumentLinkLifetime);

        return Ok(new BaseResponse<string>(url, true, string.Empty, ResponseMessages.PresignedLinkValidity));
    }

    /// <summary>Suspends a customer account (sets IsActive = false).</summary>
    /// <param name="id">Customer's database ID.</param>
    /// <response code="200">Account suspended.</response>
    /// <response code="404">Customer not found.</response>
    [HttpPut("{id:guid}/suspend")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var result = await customerCommandService.SuspendCustomer(id);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Reactivates a previously suspended customer account.</summary>
    /// <param name="id">Customer's database ID.</param>
    /// <response code="200">Account reactivated.</response>
    /// <response code="404">Customer not found.</response>
    [HttpPut("{id:guid}/reactivate")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var result = await customerCommandService.ReactivateCustomer(id);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Permanently deletes a customer account and all associated data.</summary>
    /// <param name="id">Customer's database ID.</param>
    /// <response code="204">Customer deleted.</response>
    /// <response code="404">Customer not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await customerCommandService.DeleteCustomer(id);
        if (!result.IsSuccessful) return NotFound(result);
        return NoContent();
    }
}
