using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Payments;
using HousingHub.Service.PaymentService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HousingHub.Test.Payments;

/// <summary>
/// The money that moved, totalled.
/// </summary>
/// <remarks>
/// The figure that matters is what we kept, not what passed through. Most of what
/// this system handles is rent on its way to an owner, so a report that counts
/// gross as revenue is wrong by nearly all of it.
/// </remarks>
public class AdminFinanceServiceTests
{
    private static readonly DateTime Month = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUnitOfWOrk> _unitOfWork = new() { DefaultValue = DefaultValue.Mock };
    private readonly AdminFinanceService _sut;

    public AdminFinanceServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:Currency"] = "NGN" })
            .Build();

        _sut = new AdminFinanceService(
            _unitOfWork.Object,
            new PaymentFeeCatalogue(configuration),
            NullLogger<AdminFinanceService>.Instance);
    }

    private void Setup(params Payment[] payments) =>
        _unitOfWork.Setup(u => u.PaymentQueries.GetAllAsync()).ReturnsAsync(payments);

    private static Payment Rent(
        long totalKobo, long platformKobo, DateTime paidAt, PaymentStatus status = PaymentStatus.Successful) =>
        new("HH-rent", Guid.NewGuid(), PaymentPurpose.TenancyRent, Guid.NewGuid(), totalKobo, 0, "NGN", "paystack")
        {
            Status = status,
            PaidAt = status == PaymentStatus.Successful ? paidAt : null,
            DateCreated = paidAt,
            PlatformChargeKobo = platformKobo,
        };

    private static Payment Verification(long amountKobo, DateTime paidAt) =>
        new("HH-ver", Guid.NewGuid(), PaymentPurpose.IdentityVerification, Guid.NewGuid(), amountKobo, 0, "NGN", "paystack")
        {
            Status = PaymentStatus.Successful,
            PaidAt = paidAt,
            DateCreated = paidAt,
            // Null, not zero: a verification fee was never split and is ours entirely.
            PlatformChargeKobo = null,
        };

    /// <summary>
    /// The distinction the whole report rests on. ₦5,500,000 passed through us and
    /// ₦277,000 of it was ours.
    /// </summary>
    [Fact]
    public async Task RentIsCountedAsPassingThrough_NotAsRevenue()
    {
        Setup(Rent(5_777_000_00, 277_000_00, Month.AddDays(3)));

        var report = (await _sut.GetReportAsync(Month, Month.AddMonths(1))).Data!;

        Assert.Equal(5_777_000_00, report.GrossCollectedKobo);
        Assert.Equal(5_500_000_00, report.PaidToOwnersKobo);
        Assert.Equal(277_000_00, report.EarnedKobo);
        Assert.Equal(277_000_00, report.NetEarnedKobo);
    }

    /// <summary>A verification fee was never split, so all of it is ours.</summary>
    [Fact]
    public async Task AnUnsplitPayment_IsEarnedInFull()
    {
        Setup(Verification(5_000_00, Month.AddDays(1)));

        var report = (await _sut.GetReportAsync(Month, Month.AddMonths(1))).Data!;

        Assert.Equal(5_000_00, report.GrossCollectedKobo);
        Assert.Equal(0, report.PaidToOwnersKobo);
        Assert.Equal(5_000_00, report.EarnedKobo);
    }

    /// <summary>
    /// Counted on when the money arrived, not when the attempt was opened. The bank
    /// will show it in the month it settled.
    /// </summary>
    [Fact]
    public async Task APaymentIsDatedByWhenItWasPaid()
    {
        var payment = Rent(1_000_00, 50_00, Month.AddDays(2));
        payment.DateCreated = Month.AddDays(-1);   // opened in the previous month

        Setup(payment);

        var thisMonth = (await _sut.GetReportAsync(Month, Month.AddMonths(1))).Data!;
        var lastMonth = (await _sut.GetReportAsync(Month.AddMonths(-1), Month)).Data!;

        Assert.Equal(1, thisMonth.SettledCount);
        Assert.Equal(0, lastMonth.SettledCount);
    }

    [Fact]
    public async Task PaymentsOutsideThePeriod_AreLeftOut()
    {
        Setup(
            Rent(1_000_00, 50_00, Month.AddDays(5)),
            Rent(9_000_00, 450_00, Month.AddMonths(2)));

        var report = (await _sut.GetReportAsync(Month, Month.AddMonths(1))).Data!;

        Assert.Equal(1, report.SettledCount);
        Assert.Equal(1_000_00, report.GrossCollectedKobo);
    }

    /// <summary>
    /// A refunded payment earns nothing — the whole charge went back, our share
    /// with it.
    /// </summary>
    [Fact]
    public async Task ARefundComesOffWhatWeKept()
    {
        var payment = Rent(1_000_00, 50_00, Month.AddDays(2));
        payment.RefundedAt = Month.AddDays(4);
        payment.RefundAmountKobo = 1_000_00;

        Setup(payment);

        var report = (await _sut.GetReportAsync(Month, Month.AddMonths(1))).Data!;

        Assert.Equal(1, report.RefundedCount);
        Assert.Equal(1_000_00, report.RefundedKobo);
        Assert.Equal(50_00 - 1_000_00, report.NetEarnedKobo);
    }

    /// <summary>
    /// Refunds are counted at what the provider actually sent back, which differs
    /// from the charge precisely on a flagged payment.
    /// </summary>
    [Fact]
    public async Task ARefundIsCountedAtWhatWentBack()
    {
        var payment = Rent(1_000_00, 50_00, Month.AddDays(2));
        payment.RefundedAt = Month.AddDays(4);
        payment.RefundAmountKobo = 600_00;

        Setup(payment);

        Assert.Equal(600_00, (await _sut.GetReportAsync(Month, Month.AddMonths(1))).Data!.RefundedKobo);
    }

    /// <summary>Money received against nothing handed over belongs on the report.</summary>
    [Fact]
    public async Task FlaggedPaymentsAreCountedSeparately()
    {
        Setup(Rent(1_000_00, 50_00, Month.AddDays(2), PaymentStatus.Flagged));

        var report = (await _sut.GetReportAsync(Month, Month.AddMonths(1))).Data!;

        Assert.Equal(1, report.FlaggedCount);
        Assert.Equal(1_000_00, report.FlaggedKobo);
        // Not settled, so it is nobody's revenue yet.
        Assert.Equal(0, report.EarnedKobo);
        Assert.Equal(0, report.SettledCount);
    }

    [Fact]
    public async Task AttemptsThatNeverCompleted_AreCountedButNotBanked()
    {
        Setup(
            Rent(1_000_00, 50_00, Month.AddDays(1), PaymentStatus.Pending),
            Rent(2_000_00, 100_00, Month.AddDays(1), PaymentStatus.Failed),
            Rent(3_000_00, 150_00, Month.AddDays(1), PaymentStatus.Abandoned));

        var report = (await _sut.GetReportAsync(Month, Month.AddMonths(1))).Data!;

        Assert.Equal(1, report.PendingCount);
        Assert.Equal(2, report.FailedCount);
        Assert.Equal(0, report.GrossCollectedKobo);
    }

    /// <summary>
    /// The two kinds of money behave differently, so they are totalled apart: one
    /// is ours entirely and the other is mostly somebody else's.
    /// </summary>
    [Fact]
    public async Task TotalsAreBrokenDownByPurpose()
    {
        Setup(
            Rent(5_777_000_00, 277_000_00, Month.AddDays(3)),
            Verification(5_000_00, Month.AddDays(4)));

        var report = (await _sut.GetReportAsync(Month, Month.AddMonths(1))).Data!;

        Assert.Equal(2, report.ByPurpose.Count);

        var rent = report.ByPurpose.Single(l => l.Purpose == PaymentPurpose.TenancyRent);
        Assert.Equal(5_500_000_00, rent.PaidToOwnersKobo);
        Assert.Equal(277_000_00, rent.EarnedKobo);

        var verification = report.ByPurpose.Single(l => l.Purpose == PaymentPurpose.IdentityVerification);
        Assert.Equal(0, verification.PaidToOwnersKobo);
        Assert.Equal(5_000_00, verification.EarnedKobo);
    }

    /// <summary>Defaults to the current month, which is what a report is usually asked for.</summary>
    [Fact]
    public async Task WithNoPeriodGiven_ItReportsThisMonth()
    {
        Setup();

        var report = (await _sut.GetReportAsync(null, null)).Data!;

        Assert.Equal(1, report.FromUtc.Day);
        Assert.True(report.ToUtc > report.FromUtc);
    }

    [Fact]
    public async Task ABackwardsPeriod_IsRefused()
    {
        Setup();

        var result = await _sut.GetReportAsync(Month.AddMonths(1), Month);

        Assert.False(result.IsSuccessful);
    }
}
