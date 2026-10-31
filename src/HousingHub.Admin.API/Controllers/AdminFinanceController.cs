using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.Admin.API.Controllers;

/// <summary>
/// What the money did.
/// </summary>
/// <remarks>
/// <para>
/// Read-only, with no exception. A report that can change the thing it reports on
/// is not a report, and the one action that moves money — a refund — already lives
/// on the payments controller where it is restricted to SuperAdmin.
/// </para>
/// <para>
/// No <c>[Authorize]</c>: the API's FallbackPolicy already requires an
/// authenticated caller holding <c>role=Admin</c>, so this is closed by default
/// like every other admin controller.
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminFinanceController : ControllerBase
{
    private readonly IAdminFinanceService _finance;

    public AdminFinanceController(IAdminFinanceService finance)
    {
        _finance = finance;
    }

    /// <summary>
    /// The period's totals.
    /// </summary>
    /// <remarks>
    /// Built from what each payment recorded when it was charged, never from
    /// today's rates — otherwise last month's report changes the next time a rate
    /// does, and the figures stop matching the bank.
    /// </remarks>
    /// <param name="from">Inclusive, UTC. Defaults to the start of the current month.</param>
    /// <param name="to">Exclusive, UTC. Defaults to now.</param>
    [HttpGet("report")]
    [ProducesResponseType(typeof(BaseResponse<FinanceReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        return Ok(await _finance.GetReportAsync(from, to));
    }
}
