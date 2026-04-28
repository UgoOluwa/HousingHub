using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
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
    ICustomerCommandService customerCommandService) : ControllerBase
{
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
    /// <response code="200">KYC decision applied.</response>
    /// <response code="404">Customer not found.</response>
    [HttpPut("{id:guid}/kyc/verify")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyKyc(Guid id, [FromQuery] bool approve)
    {
        var result = await customerCommandService.VerifyKyc(id, approve);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
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
