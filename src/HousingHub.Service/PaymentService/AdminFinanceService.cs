using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Payments;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PaymentService;

/// <inheritdoc cref="IAdminFinanceService"/>
public class AdminFinanceService : IAdminFinanceService
{
    private readonly IUnitOfWOrk _unitOfWork;
    private readonly PaymentFeeCatalogue _fees;
    private readonly ILogger<AdminFinanceService> _logger;

    public AdminFinanceService(
        IUnitOfWOrk unitOfWork,
        PaymentFeeCatalogue fees,
        ILogger<AdminFinanceService> logger)
    {
        _unitOfWork = unitOfWork;
        _fees = fees;
        _logger = logger;
    }

    public async Task<BaseResponse<FinanceReportDto>> GetReportAsync(DateTime? fromUtc, DateTime? toUtc)
    {
        try
        {
            var to = toUtc ?? DateTime.UtcNow;
            var from = fromUtc ?? new DateTime(to.Year, to.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            if (from >= to)
                return Fail("The start of the period has to come before the end of it.");

            // A full read, then filtered in memory — the same shape as the admin
            // payment queue, and for the same reason: there is no date index to push
            // this down to. Fine at this volume; a date-bucketed index is the answer
            // if the table grows past a few thousand rows.
            var all = await _unitOfWork.PaymentQueries.GetAllAsync();

            // Settled payments are counted on when the money arrived, not when the
            // attempt was created. An attempt started on the last day of a month and
            // paid on the first of the next belongs to the month it was paid in,
            // which is the month the bank will show it in.
            var inPeriod = all
                .Where(p => Moment(p) >= from && Moment(p) < to)
                .ToList();

            var settled = inPeriod.Where(p => p.Status == PaymentStatus.Successful).ToList();
            var refunded = inPeriod.Where(p => p.RefundedAt is { } at && at >= from && at < to).ToList();
            var flagged = inPeriod.Where(p => p.Status == PaymentStatus.Flagged).ToList();

            var earned = settled.Sum(p => p.PlatformEarnedKobo);

            // What the provider actually sent back, not what was asked for. The two
            // differ precisely on a flagged payment, which is the commonest reason
            // to refund one.
            var refundedKobo = refunded.Sum(p => p.RefundAmountKobo ?? 0);

            var report = new FinanceReportDto(
                FromUtc: from,
                ToUtc: to,
                Currency: _fees.Currency,
                SettledCount: settled.Count,
                GrossCollectedKobo: settled.Sum(p => p.AmountKobo),
                PaidToOwnersKobo: settled.Sum(p => p.SettledToOthersKobo),
                EarnedKobo: earned,
                RefundedKobo: refundedKobo,
                RefundedCount: refunded.Count,
                NetEarnedKobo: earned - refundedKobo,
                FlaggedCount: flagged.Count,
                FlaggedKobo: flagged.Sum(p => p.AmountKobo),
                PendingCount: inPeriod.Count(p => p.Status == PaymentStatus.Pending),
                FailedCount: inPeriod.Count(p =>
                    p.Status is PaymentStatus.Failed or PaymentStatus.Abandoned),
                ByPurpose: settled
                    .GroupBy(p => p.Purpose)
                    .Select(g => new FinancePurposeLineDto(
                        g.Key,
                        g.Count(),
                        g.Sum(p => p.AmountKobo),
                        g.Sum(p => p.SettledToOthersKobo),
                        g.Sum(p => p.PlatformEarnedKobo)))
                    .OrderByDescending(line => line.EarnedKobo)
                    .ToList());

            return new BaseResponse<FinanceReportDto>(report, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building the finance report");
            return Fail(ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// When a payment belongs to the period.
    /// </summary>
    /// <remarks>
    /// The moment the money arrived, falling back to when the attempt was created
    /// for everything that never completed. Dating a settled payment by its creation
    /// would file it in the month somebody opened the checkout, which is not the
    /// month the bank will show it in.
    /// </remarks>
    private static DateTime Moment(Payment payment) => payment.PaidAt ?? payment.DateCreated;

    private static BaseResponse<FinanceReportDto> Fail(string message) =>
        new(null, false, string.Empty, message);
}
