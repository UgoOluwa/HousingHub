using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Payments;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PaymentService;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HousingHub.Test.Payments;

/// <summary>
/// The refund path — the only thing in this system that moves money out.
/// </summary>
public class AdminRefundTests
{
    private const long AmountKobo = 750_000;
    private const string Reason = "Documents rejected as a duplicate submission";
    private const string Reference = "HH-refundme";

    private static readonly Guid AdminId = Guid.NewGuid();

    private readonly Mock<IUnitOfWOrk> _unitOfWork = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IAdminPaymentQueryService> _query = new();
    private readonly List<Payment> _updated = [];

    private readonly AdminPaymentCommandService _sut;

    public AdminRefundTests()
    {
        _unitOfWork
            .Setup(u => u.PaymentCommands.UpdateAsync(It.IsAny<Payment>()))
            .Callback<Payment>(_updated.Add)
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(u => u.PaymentQueries.QueryByIndexAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new List<Payment>());

        // The service returns the refreshed DTO through the query service; these
        // tests assert on the entity and the gateway, so a success envelope is enough.
        _query
            .Setup(q => q.GetByReferenceAsync(It.IsAny<string>()))
            .ReturnsAsync(new BaseResponse<AdminPaymentDto>(
                null, true, string.Empty, ResponseMessages.Successful));

        _sut = new AdminPaymentCommandService(
            _unitOfWork.Object, _gateway.Object, _query.Object,
            NullLogger<AdminPaymentCommandService>.Instance);
    }

    private Payment GivenPayment(PaymentStatus status, long confirmedKobo = AmountKobo)
    {
        var payment = new Payment(
            Reference, Guid.NewGuid(), PaymentPurpose.BusinessVerification, Guid.NewGuid(),
            500_000, 250_000, "NGN", "paystack");

        // Drive it into the requested state through the real transitions, so the
        // fixture cannot describe a state the entity would never produce.
        if (status is PaymentStatus.Successful or PaymentStatus.Flagged)
            payment.TrySettle(status == PaymentStatus.Flagged ? confirmedKobo : payment.AmountKobo, "pstk_1", "card");
        else if (status is PaymentStatus.Failed or PaymentStatus.Abandoned)
            payment.TryFail(status, "declined");
        else if (status == PaymentStatus.Refunded)
        {
            payment.TrySettle(payment.AmountKobo, "pstk_1", "card");
            payment.TryBeginRefund(payment.AmountKobo, Reason, AdminId);
            payment.TryCompleteRefund(payment.AmountKobo, "refund_1");
        }
        else if (status == PaymentStatus.RefundPending)
        {
            payment.TrySettle(payment.AmountKobo, "pstk_1", "card");
            payment.TryBeginRefund(payment.AmountKobo, Reason, AdminId);
        }

        _unitOfWork
            .Setup(u => u.PaymentQueries.QueryByIndexAsync("Reference-index", It.IsAny<object>()))
            .ReturnsAsync(new List<Payment> { payment });

        return payment;
    }

    private void GivenProviderConfirms(long amountKobo = AmountKobo) =>
        _gateway
            .Setup(g => g.GetTransactionAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayTransaction(
                Reference, GatewayTransactionStatus.Successful, amountKobo, "pstk_1", "card", null));

    private void GivenRefundAccepted(bool isComplete = false) =>
        _gateway
            .Setup(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayRefund(true, isComplete, AmountKobo, "refund_9", null));

    // ── guards ───────────────────────────────────────────────────

    /// <summary>
    /// A refund is the one action here that needs to be explainable months later,
    /// when the question is why money left the account.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("n/a")]
    [InlineData("asked")]
    public async Task Refund_WithoutARealReason_IsRefused(string reason)
    {
        GivenPayment(PaymentStatus.Successful);

        var result = await _sut.RefundAsync(Reference, reason, AdminId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.RefundReasonRequired, result.Message);
        _gateway.Verify(
            g => g.RefundAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>An unattributable refund is not one we should be able to issue.</summary>
    [Fact]
    public async Task Refund_WithNoAdminIdentity_IsRefused()
    {
        GivenPayment(PaymentStatus.Successful);

        var result = await _sut.RefundAsync(Reference, Reason, Guid.Empty);

        Assert.False(result.IsSuccessful);
        _gateway.Verify(
            g => g.RefundAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Refund_OfAnUnknownReference_ReportsNotFound()
    {
        var result = await _sut.RefundAsync("HH-nope", Reason, AdminId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.SetNotFoundMessage("payment"), result.Message);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Abandoned)]
    public async Task Refund_OfAPaymentThatNeverSucceeded_IsRefused(PaymentStatus status)
    {
        GivenPayment(status);

        var result = await _sut.RefundAsync(Reference, Reason, AdminId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.RefundNotPossible, result.Message);
    }

    [Theory]
    [InlineData(PaymentStatus.RefundPending)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task Refund_WhenOneIsAlreadyInFlightOrDone_IsRefused(PaymentStatus status)
    {
        GivenPayment(status);

        var result = await _sut.RefundAsync(Reference, Reason, AdminId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.RefundAlreadyInProgress, result.Message);
        _gateway.Verify(
            g => g.RefundAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Never refund on our own say-so. If the provider does not report a successful
    /// charge, sending money back would be sending money we may never have received.
    /// </summary>
    [Fact]
    public async Task Refund_WhenTheProviderDoesNotConfirmTheCharge_IsRefused()
    {
        GivenPayment(PaymentStatus.Successful);
        _gateway
            .Setup(g => g.GetTransactionAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GatewayTransaction?)null);

        var result = await _sut.RefundAsync(Reference, Reason, AdminId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.RefundNotConfirmedByProvider, result.Message);
        _gateway.Verify(
            g => g.RefundAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── the happy paths ──────────────────────────────────────────

    [Fact]
    public async Task Refund_RequestsFromTheProviderAndRecordsIt()
    {
        var payment = GivenPayment(PaymentStatus.Successful);
        GivenProviderConfirms();
        GivenRefundAccepted();

        var result = await _sut.RefundAsync(Reference, Reason, AdminId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(PaymentStatus.RefundPending, payment.Status);
        Assert.Equal(Reason, payment.RefundReason);
        Assert.Equal(AdminId, payment.RefundedByAdminId);

        // Not refunded yet — the provider answered "pending" and will confirm.
        Assert.Null(payment.RefundedAt);
    }

    [Fact]
    public async Task Refund_WhenTheProviderCompletesImmediately_RecordsItAsRefunded()
    {
        var payment = GivenPayment(PaymentStatus.Successful);
        GivenProviderConfirms();
        GivenRefundAccepted(isComplete: true);

        var result = await _sut.RefundAsync(Reference, Reason, AdminId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.NotNull(payment.RefundedAt);
    }

    /// <summary>
    /// The reason this feature exists. A flagged payment's confirmed amount is not
    /// the amount we asked for, and refunding our own figure would send back money
    /// nobody paid.
    /// </summary>
    [Fact]
    public async Task Refund_OfAFlaggedPayment_SendsBackWhatActuallyArrived()
    {
        var payment = GivenPayment(PaymentStatus.Flagged, confirmedKobo: 100);
        GivenProviderConfirms(amountKobo: 100);
        GivenRefundAccepted();

        var result = await _sut.RefundAsync(Reference, Reason, AdminId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(100, payment.RefundAmountKobo);
        Assert.NotEqual(payment.AmountKobo, payment.RefundAmountKobo);

        _gateway.Verify(
            g => g.RefundAsync(Reference, 100, Reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Claimed before the provider is contacted, so a second click finds a refund in
    /// flight rather than sending another one.
    /// </summary>
    [Fact]
    public async Task Refund_ClaimsThePaymentBeforeContactingTheProvider()
    {
        var payment = GivenPayment(PaymentStatus.Successful);
        GivenProviderConfirms();

        PaymentStatus statusWhenGatewayCalled = PaymentStatus.Pending;
        _gateway
            .Setup(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => statusWhenGatewayCalled = payment.Status)
            .ReturnsAsync(new GatewayRefund(true, false, AmountKobo, "refund_9", null));

        await _sut.RefundAsync(Reference, Reason, AdminId);

        Assert.Equal(PaymentStatus.RefundPending, statusWhenGatewayCalled);
    }

    /// <summary>
    /// And if the provider then refuses, the claim is released — otherwise the
    /// payment sits forever pending a refund nobody asked for.
    /// </summary>
    [Fact]
    public async Task Refund_WhenTheProviderRefuses_ReleasesThePayment()
    {
        var payment = GivenPayment(PaymentStatus.Successful);
        GivenProviderConfirms();
        _gateway
            .Setup(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GatewayRefund.Failed("insufficient balance"));

        var result = await _sut.RefundAsync(Reference, Reason, AdminId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Null(payment.RefundRequestedAt);
    }

    /// <summary>A flagged payment whose refund was refused returns to the admin queue.</summary>
    [Fact]
    public async Task Refund_OfAFlaggedPayment_WhenRefused_ReturnsItToTheQueue()
    {
        var payment = GivenPayment(PaymentStatus.Flagged, confirmedKobo: 100);
        GivenProviderConfirms(amountKobo: 100);
        _gateway
            .Setup(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GatewayRefund.Failed("insufficient balance"));

        await _sut.RefundAsync(Reference, Reason, AdminId);

        Assert.Equal(PaymentStatus.Flagged, payment.Status);
        Assert.Equal(Payment.FlaggedMarker, payment.FlagWatch);
    }

    /// <summary>The reason reaches the provider, so their record and ours agree.</summary>
    [Fact]
    public async Task Refund_PassesTheReasonToTheProvider()
    {
        GivenPayment(PaymentStatus.Successful);
        GivenProviderConfirms();
        GivenRefundAccepted();

        await _sut.RefundAsync(Reference, Reason, AdminId);

        _gateway.Verify(
            g => g.RefundAsync(Reference, AmountKobo, Reason, It.IsAny<CancellationToken>()), Times.Once);
    }
}
