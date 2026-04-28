using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Customer;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>Manage house owners and agents (CustomerType = HouseOwner or Agent).</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminOwnerController(
    ICustomerQueryService customerQueryService,
    ICustomerCommandService customerCommandService) : ControllerBase
{
    /// <summary>Returns a filtered, paginated list of house owners and agents.</summary>
    /// <remarks>
    /// Only returns accounts with CustomerType HouseOwner or Agent.
    /// Supports search by name, email, or phone number; filter by KYC verification status and active status.
    /// </remarks>
    /// <param name="filter">Search and filter parameters.</param>
    /// <param name="type">Optional: 1 = HouseOwner only, 2 = Agent only. Omit for both.</param>
    /// <response code="200">Paginated list of owners/agents.</response>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<AdminCustomerListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminCustomerFilterDto filter,
        [FromQuery] CustomerType? type = null)
    {
        // Default scope: HouseOwner or Agent. If caller specifies one, use it directly.
        CustomerType? typeFlag = type is CustomerType.HouseOwner or CustomerType.Agent ? type : null;

        if (typeFlag == null)
        {
            // Return both HouseOwner and Agent by running two queries and merging
            var ownersTask = customerQueryService.GetCustomersFilteredAsync(
                filter with { PageNumber = 1, PageSize = int.MaxValue }, CustomerType.HouseOwner);
            var agentsTask = customerQueryService.GetCustomersFilteredAsync(
                filter with { PageNumber = 1, PageSize = int.MaxValue }, CustomerType.Agent);

            await Task.WhenAll(ownersTask, agentsTask);

            var combined = (ownersTask.Result.Data?.Items ?? [])
                .Concat(agentsTask.Result.Data?.Items ?? [])
                .OrderByDescending(c => c.DateJoined)
                .ToList();

            var totalCount = combined.Count;
            var paged = combined
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            return Ok(new BaseResponse<PaginatedResult<AdminCustomerListDto>>(
                new PaginatedResult<AdminCustomerListDto>(paged, totalCount, filter.PageNumber, filter.PageSize),
                true, string.Empty, "Successful"));
        }

        var result = await customerQueryService.GetCustomersFilteredAsync(filter, typeFlag);
        return Ok(result);
    }

    /// <summary>Returns full details of a single owner/agent including KYC and personal info.</summary>
    /// <param name="id">Owner/agent's database ID.</param>
    /// <response code="200">Owner/agent details.</response>
    /// <response code="404">Not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<CustomerWithDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await customerQueryService.GetCustomerAsync(id);
        if (result.Data == null) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Approves or rejects an owner/agent's KYC submission.</summary>
    /// <param name="id">Owner/agent's database ID.</param>
    /// <param name="approve">True to approve, false to reject.</param>
    /// <response code="200">KYC decision applied.</response>
    /// <response code="404">Not found.</response>
    [HttpPut("{id:guid}/kyc/verify")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyKyc(Guid id, [FromQuery] bool approve)
    {
        var result = await customerCommandService.VerifyKyc(id, approve);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Suspends an owner/agent account (sets IsActive = false).</summary>
    /// <param name="id">Owner/agent's database ID.</param>
    /// <response code="200">Account suspended.</response>
    /// <response code="404">Not found.</response>
    [HttpPut("{id:guid}/suspend")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var result = await customerCommandService.SuspendCustomer(id);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Reactivates a previously suspended owner/agent account.</summary>
    /// <param name="id">Owner/agent's database ID.</param>
    /// <response code="200">Account reactivated.</response>
    /// <response code="404">Not found.</response>
    [HttpPut("{id:guid}/reactivate")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var result = await customerCommandService.ReactivateCustomer(id);
        if (!result.IsSuccessful) return NotFound(result);
        return Ok(result);
    }
}
