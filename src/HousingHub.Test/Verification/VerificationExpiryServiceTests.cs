using System.Linq.Expressions;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.VerificationService;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PropertyEntity = HousingHub.Model.Entities.Property;

namespace HousingHub.Test.Verification;

/// <summary>
/// The sweep that takes badges back when their evidence lapses.
/// </summary>
/// <remarks>
/// Without this, a badge granted once is granted forever — which is the same
/// failure as not verifying at all, only slower and more convincing to whoever
/// relies on it. The failure is also completely silent, so it is worth pinning
/// down properly.
/// </remarks>
public class VerificationExpiryServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWork;
    private readonly Mock<IEmailService> _email;
    private readonly VerificationExpiryService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private static readonly DateTime Now = new(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);

    public VerificationExpiryServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _email = new Mock<IEmailService>();

        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.VerificationCaseCommands.UpdateAsync(It.IsAny<VerificationCase>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CustomerCommands.UpdateAsync(It.IsAny<Customer>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<PropertyEntity>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>()))
            .ReturnsAsync(true);

        _sut = new VerificationExpiryService(
            _unitOfWork.Object,
            _email.Object,
            new Mock<IRealtimeNotifier>().Object,
            NullLogger<VerificationExpiryService>.Instance);
    }

    private void GivenWatchedCases(params VerificationCase[] cases)
    {
        _unitOfWork
            .Setup(u => u.VerificationCaseQueries.GetAllAsync(
                It.IsAny<Expression<Func<VerificationCase, bool>>>()))
            .ReturnsAsync(cases.ToList());
    }

    private static VerificationCase ApprovedCase(
        VerificationSubjectType subjectType, Guid subjectId, DateTime? expiresAt)
    {
        var verificationCase = new VerificationCase(
            subjectId, subjectType, CustomerId, VerificationRequirements.TierFor(subjectType));

        verificationCase.TrySubmit();
        verificationCase.TryDecide(VerificationCaseStatus.Approved, AdminId, null, expiresAt);

        return verificationCase;
    }

    private Customer GivenVerifiedCustomer(DateTime? expiresAt)
    {
        var customer = new Customer("Ada", "Obi", "ada@test.com", "08012345678", CustomerType.Agent, "hash")
        {
            Id = CustomerId,
            BusinessVerificationTier = VerificationTier.BusinessVerified,
            BusinessVerifiedAt = Now.AddYears(-1),
            BusinessVerificationExpiresAt = expiresAt,
        };

        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(CustomerId)).ReturnsAsync(customer);
        return customer;
    }

    private PropertyEntity GivenVerifiedProperty()
    {
        var property = new PropertyEntity("Flat", "Desc", PropertyType.Apartment, 100m,
            PropertyAvailability.Available, PropertyLeaseType.Rent)
        {
            Id = PropertyId,
            OwnerId = CustomerId,
            TitleVerificationTier = VerificationTier.TitleVerified,
        };

        _unitOfWork.Setup(u => u.PropertyQueries.GetByIdAsync(PropertyId)).ReturnsAsync(property);
        return property;
    }

    // ── The core behaviour ───────────────────────────────────────

    [Fact]
    public async Task ALapsedBusinessCase_IsExpiredAndTheTierIsRevoked()
    {
        var customer = GivenVerifiedCustomer(Now.AddDays(-1));
        var lapsed = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(-1));
        GivenWatchedCases(lapsed);

        var summary = await _sut.ExpireLapsedAsync(Now);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(1, summary.TiersRevoked);
        Assert.Equal(VerificationCaseStatus.Expired, lapsed.Status);

        // The case status alone is not enough. Anything reading the tier directly
        // would still see BusinessVerified if this were not cleared.
        Assert.Equal(VerificationTier.Unverified, customer.BusinessVerificationTier);
        Assert.False(customer.IsBusinessVerified);
    }

    [Fact]
    public async Task RevokingTheTier_KeepsTheHistoricalTimestamp()
    {
        // BusinessVerifiedAt records that a check happened, which stays true. It is
        // the entitlement that lapses, not the fact of the review.
        var customer = GivenVerifiedCustomer(Now.AddDays(-1));
        var verifiedAt = customer.BusinessVerifiedAt;
        GivenWatchedCases(ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(-1)));

        await _sut.ExpireLapsedAsync(Now);

        Assert.Equal(verifiedAt, customer.BusinessVerifiedAt);
    }

    [Fact]
    public async Task ACaseThatHasNotExpiredYet_IsLeftAlone()
    {
        var customer = GivenVerifiedCustomer(Now.AddMonths(6));
        var current = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddMonths(6));
        GivenWatchedCases(current);

        var summary = await _sut.ExpireLapsedAsync(Now);

        Assert.Equal(0, summary.Expired);
        Assert.Equal(VerificationCaseStatus.Approved, current.Status);
        Assert.Equal(VerificationTier.BusinessVerified, customer.BusinessVerificationTier);
    }

    [Fact]
    public async Task ALapsedTitleCase_RevokesTheTitleTier()
    {
        GivenVerifiedCustomer(null);
        var property = GivenVerifiedProperty();
        GivenWatchedCases(ApprovedCase(VerificationSubjectType.Property, PropertyId, Now.AddDays(-1)));

        var summary = await _sut.ExpireLapsedAsync(Now);

        Assert.Equal(1, summary.TiersRevoked);
        Assert.Equal(VerificationTier.Unverified, property.TitleVerificationTier);
        Assert.False(property.IsTitleVerified);
    }

    [Fact]
    public async Task AnIdentityCase_NeverRevokesKyc()
    {
        // Identity is verified against NIN or BVN, which do not lapse. Revoking KYC
        // over a bookkeeping detail would lock somebody out of publishing.
        var customer = GivenVerifiedCustomer(null);
        customer.UpdateKycStatus(isVerified: true);

        GivenWatchedCases(ApprovedCase(VerificationSubjectType.Identity, CustomerId, Now.AddDays(-1)));

        var summary = await _sut.ExpireLapsedAsync(Now);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.TiersRevoked);
        Assert.True(customer.IsKycVerified);
    }

    // ── Robustness ───────────────────────────────────────────────

    [Fact]
    public async Task OneFailure_DoesNotStopTheRest()
    {
        // If a bad row aborted the run, every case after it would keep a badge it is
        // no longer entitled to until somebody noticed.
        var missingSubject = Guid.NewGuid();
        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(missingSubject))
            .ThrowsAsync(new InvalidOperationException("boom"));

        GivenVerifiedCustomer(Now.AddDays(-1));

        var broken = ApprovedCase(VerificationSubjectType.Business, missingSubject, Now.AddDays(-1));
        var good = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(-1));
        GivenWatchedCases(broken, good);

        var summary = await _sut.ExpireLapsedAsync(Now);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(VerificationCaseStatus.Expired, good.Status);
    }

    [Fact]
    public async Task RunningTwice_ExpiresNothingTheSecondTime()
    {
        // The scheduler may fire more than once, or be retried. A second pass must be
        // a no-op rather than re-notifying everybody.
        GivenVerifiedCustomer(Now.AddDays(-1));
        var lapsed = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(-1));
        GivenWatchedCases(lapsed);

        await _sut.ExpireLapsedAsync(Now);
        var second = await _sut.ExpireLapsedAsync(Now);

        Assert.Equal(0, second.Expired);
        _email.Verify(
            e => e.SendVerificationExpiredAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task NothingWatched_IsAnEmptyRunRatherThanAnError()
    {
        GivenWatchedCases();

        var summary = await _sut.ExpireLapsedAsync(Now);

        Assert.Equal(0, summary.Examined);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task TheSubjectIsToldTheirVerificationLapsed()
    {
        GivenVerifiedCustomer(Now.AddDays(-1));
        GivenWatchedCases(ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(-1)));

        await _sut.ExpireLapsedAsync(Now);

        // A distinct email from the rejection one. Nothing was wrong with the
        // submission — it aged out — and somebody who reads it as a rejection goes
        // looking for a mistake they did not make.
        _email.Verify(
            e => e.SendVerificationExpiredAsync("ada@test.com", "Ada", It.IsAny<string>()),
            Times.Once);

        _unitOfWork.Verify(u => u.NotificationCommands.InsertAsync(
            It.Is<Notification>(n => n.Type == NotificationType.VerificationExpired)), Times.Once);
    }

    // ── The index key that makes the sweep cheap ─────────────────

    [Fact]
    public void OnlyApprovedCasesWithAnExpiryAreWatched()
    {
        // The sweep reads a sparse index. If this marker appeared on cases that can
        // never lapse, the nightly run would read every case ever approved.
        var withExpiry = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddYears(1));
        var withoutExpiry = ApprovedCase(VerificationSubjectType.Property, PropertyId, null);

        var draft = new VerificationCase(
            CustomerId, VerificationSubjectType.Business, CustomerId, VerificationTier.BusinessVerified);

        var rejected = new VerificationCase(
            CustomerId, VerificationSubjectType.Business, CustomerId, VerificationTier.BusinessVerified);
        rejected.TrySubmit();
        rejected.TryDecide(VerificationCaseStatus.Rejected, AdminId, "No.");

        Assert.Equal(VerificationCase.ExpiryWatchMarker, withExpiry.ExpiryWatch);
        Assert.Null(withoutExpiry.ExpiryWatch);
        Assert.Null(draft.ExpiryWatch);
        Assert.Null(rejected.ExpiryWatch);
    }

    [Fact]
    public void TheWatchMarkerIsDerived_AndIgnoresWhatTheMapperWritesBack()
    {
        var watched = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddYears(1));

        watched.ExpiryWatch = null;
        Assert.Equal(VerificationCase.ExpiryWatchMarker, watched.ExpiryWatch);

        watched.TryExpire(Now.AddYears(2));
        watched.ExpiryWatch = VerificationCase.ExpiryWatchMarker;
        Assert.Null(watched.ExpiryWatch);
    }

    // ── Reminders ────────────────────────────────────────────────

    [Fact]
    public async Task ACaseThirtyDaysOut_GetsTheThirtyDayWarning()
    {
        GivenVerifiedCustomer(Now.AddDays(30));
        var soon = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(30));
        GivenWatchedCases(soon);

        var summary = await _sut.SendExpiryRemindersAsync(Now);

        Assert.Equal(1, summary.Sent);
        Assert.Equal(30, soon.LastExpiryReminderThreshold);
        _email.Verify(e => e.SendVerificationExpiringSoonAsync(
            "ada@test.com", "Ada", It.IsAny<string>(), 30, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ACaseFortyDaysOut_IsNotWarnedYet()
    {
        GivenVerifiedCustomer(Now.AddDays(40));
        GivenWatchedCases(ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(40)));

        var summary = await _sut.SendExpiryRemindersAsync(Now);

        Assert.Equal(0, summary.Sent);
    }

    [Fact]
    public async Task RunningDailyForAMonth_SendsTwoRemindersNotThirty()
    {
        // The whole reason the threshold is stored. A worker running every day
        // without it would send the same warning every day for a month, and people
        // filter a sender who does that.
        GivenVerifiedCustomer(Now.AddDays(31));
        var soon = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(31));
        GivenWatchedCases(soon);

        for (var day = 0; day <= 30; day++)
        {
            await _sut.SendExpiryRemindersAsync(Now.AddDays(day));
        }

        _email.Verify(e => e.SendVerificationExpiringSoonAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>()),
            Times.Exactly(2));

        Assert.Equal(7, soon.LastExpiryReminderThreshold);
    }

    [Fact]
    public async Task ACaseAlreadyInsideSevenDays_GetsTheSevenDayNudgeNotTheThirty()
    {
        // Somebody verified last week with a permit expiring in three days should
        // get the urgent warning, not the one they already slept through.
        GivenVerifiedCustomer(Now.AddDays(3));
        var urgent = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(3));
        GivenWatchedCases(urgent);

        await _sut.SendExpiryRemindersAsync(Now);

        Assert.Equal(7, urgent.LastExpiryReminderThreshold);
        _email.Verify(e => e.SendVerificationExpiringSoonAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 3, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task AnAlreadyLapsedCase_IsNotSentAReminder()
    {
        // It gets the expiry email instead. Warning somebody that something expires
        // "in -2 days" is worse than saying nothing.
        GivenVerifiedCustomer(Now.AddDays(-2));
        GivenWatchedCases(ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(-2)));

        var summary = await _sut.SendExpiryRemindersAsync(Now);

        Assert.Equal(0, summary.Sent);
    }

    [Fact]
    public async Task AFailedReminder_IsRetriedOnTheNextRun()
    {
        // The threshold is recorded only after a successful send, so a mail outage
        // delays the warning rather than losing it.
        GivenVerifiedCustomer(Now.AddDays(30));
        var soon = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(30));
        GivenWatchedCases(soon);

        _email.Setup(e => e.SendVerificationExpiringSoonAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("mail down"));

        var first = await _sut.SendExpiryRemindersAsync(Now);

        Assert.Equal(1, first.Failed);
        Assert.Null(soon.LastExpiryReminderThreshold);

        _email.Reset();
        _email.Setup(e => e.SendVerificationExpiringSoonAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        var second = await _sut.SendExpiryRemindersAsync(Now.AddDays(1));

        Assert.Equal(1, second.Sent);
        Assert.Equal(30, soon.LastExpiryReminderThreshold);
    }

    [Fact]
    public void NeedsExpiryReminder_TracksWhichThresholdsRemain()
    {
        var item = ApprovedCase(VerificationSubjectType.Business, CustomerId, Now.AddDays(60));

        Assert.True(item.NeedsExpiryReminder(30));
        Assert.True(item.NeedsExpiryReminder(7));

        item.MarkExpiryReminderSent(30);
        Assert.False(item.NeedsExpiryReminder(30));
        Assert.True(item.NeedsExpiryReminder(7));

        item.MarkExpiryReminderSent(7);
        Assert.False(item.NeedsExpiryReminder(7));
    }
}
