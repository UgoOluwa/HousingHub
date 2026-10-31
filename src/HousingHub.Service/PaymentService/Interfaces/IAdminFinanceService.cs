using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Payment;

namespace HousingHub.Service.PaymentService.Interfaces;

/// <summary>
/// What the money did, for staff.
/// </summary>
/// <remarks>
/// Read-only, like every other admin view of payments. Nothing here can create,
/// settle or adjust anything — a report that could change the thing it reports on
/// is not a report.
/// </remarks>
public interface IAdminFinanceService
{
    /// <summary>
    /// The period's totals, built from what each payment recorded when it was charged.
    /// </summary>
    /// <param name="fromUtc">Inclusive. Defaults to the start of the current month.</param>
    /// <param name="toUtc">Exclusive. Defaults to now.</param>
    Task<BaseResponse<FinanceReportDto>> GetReportAsync(DateTime? fromUtc, DateTime? toUtc);
}
