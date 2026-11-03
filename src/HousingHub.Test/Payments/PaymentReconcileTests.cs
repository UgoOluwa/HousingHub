using System.Linq.Expressions;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.Payments;
using HousingHub.Service.PaymentService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HousingHub.Test.Payments;

/// <summary>
/// Asking the provider what happened, when the payer comes back.
/// </summary>
/// <remarks>
/// Without this the system depended entirely on the webhook arriving: somebody who
/// had just paid returned to a page saying they had not, and the obvious response
/// to that is to pay again.
/// </remarks>
public class PaymentReconcileTests
{
    private const string Reference = "HH-abc123";
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid TenancyId = Guid.NewGuid();

    private readonly Mock<IUnitOfWOrk> _unitOfWork = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly PaymentService _sut;

    public PaymentReconcileTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:Enabled"] = "true" })
            .Build();

        _sut = new PaymentService(
            _unitOfWork.Object,
            _gateway.Object,
            new PaymentCallbackUrlResolver(configuration, NullLogger<PaymentCallbackUrlResolver>.Instance),
            new PaymentFeeCatalogue(configuration),
            _email.Object,
            configuration,
            NullLogger<PaymentService>.Instance);

        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
    }

    private Payment Setup(PaymentStatus status, long amountKobo = 1_000_00)
    {
        var payment = new Payment(
            Reference, CustomerId, PaymentPurpose.TenancyRent, TenancyId, amountKobo, 0, "NGN", "paystack")
        {
            Status = status,
        };

        _unitOfWork.Setup(u => u.PaymentQueries.QueryByIndexAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync([payment]);
        _unitOfWork.Setup(u => u.PaymentQueries.GetByAsync(It.IsAny<Expression<Func<Payment, bool>>>()))
            .ReturnsAsync(payment);

        return payment;
    }

    private void GatewaySays(GatewayTransactionStatus status, long amountKobo = 1_000_00) =>
        _gateway.Setup(g => g.GetTransactionAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayTransaction(Reference, status, amountKobo, "ps_ref", "card", null));

    [Fact]
    public async Task APendingPaymentTheProviderCallsPaid_IsSettled()
    {
        var payment = Setup(PaymentStatus.Pending);
        GatewaySays(GatewayTransactionStatus.Successful);

        var result = await _sut.ReconcileAsync(CustomerId, Reference);

        Assert.True(result.IsSuccessful);
        Assert.Equal(PaymentStatus.Successful, payment.Status);
    }

    /// <summary>
    /// The tenancy is what the payment was for, so reconciling has to hand it over
    /// exactly as the webhook would.
    /// </summary>
    [Fact]
    public async Task SettlingOnReturn_ActivatesTheTenancy()
    {
        Setup(PaymentStatus.Pending);
        GatewaySays(GatewayTransactionStatus.Successful);

        var tenancy = new Tenancy(
            Guid.NewGuid(), Guid.NewGuid(), CustomerId, Guid.NewGuid(), 1_000_00, PropertyLeaseType.Rent)
        {
            Id = TenancyId,
            Status = TenancyStatus.AwaitingPayment,
        };
        _unitOfWork.Setup(u => u.TenancyQueries.GetByIdAsync(TenancyId)).ReturnsAsync(tenancy);

        await _sut.ReconcileAsync(CustomerId, Reference);

        Assert.Equal(TenancyStatus.Active, tenancy.Status);
    }

    /// <summary>
    /// Whichever of the webhook and the return arrives second must change nothing.
    /// Both land on TrySettle, which only ever moves a payment out of Pending.
    /// </summary>
    [Fact]
    public async Task APaymentTheWebhookAlreadySettled_IsNotTouched()
    {
        Setup(PaymentStatus.Successful);

        var result = await _sut.ReconcileAsync(CustomerId, Reference);

        Assert.True(result.IsSuccessful);
        _gateway.Verify(g => g.GetTransactionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A refunded payment must not be reported as paid by asking again.</summary>
    [Fact]
    public async Task ARefundedPayment_IsNotReExamined()
    {
        Setup(PaymentStatus.Refunded);

        await _sut.ReconcileAsync(CustomerId, Reference);

        _gateway.Verify(g => g.GetTransactionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task APaymentTheProviderCallsAbandoned_IsNotSettled()
    {
        var payment = Setup(PaymentStatus.Pending);
        GatewaySays(GatewayTransactionStatus.Abandoned);

        await _sut.ReconcileAsync(CustomerId, Reference);

        Assert.NotEqual(PaymentStatus.Successful, payment.Status);
    }

    /// <summary>
    /// Confirmed for the wrong amount is flagged, not handed over — reconciling is
    /// not a softer route to settlement than the webhook.
    /// </summary>
    [Fact]
    public async Task APaymentConfirmedForTheWrongAmount_IsFlagged()
    {
        var payment = Setup(PaymentStatus.Pending, amountKobo: 1_000_00);
        GatewaySays(GatewayTransactionStatus.Successful, amountKobo: 10_00);

        await _sut.ReconcileAsync(CustomerId, Reference);

        Assert.Equal(PaymentStatus.Flagged, payment.Status);
    }

    /// <summary>
    /// Somebody else's reference answers the same as one that does not exist,
    /// otherwise this is an oracle for guessing them.
    /// </summary>
    [Fact]
    public async Task SomebodyElsesPayment_IsNotFound()
    {
        Setup(PaymentStatus.Pending);

        var result = await _sut.ReconcileAsync(Guid.NewGuid(), Reference);

        Assert.False(result.IsSuccessful);
        _gateway.Verify(g => g.GetTransactionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── telling the owner ────────────────────────────────────────

    private Tenancy SetupTenancy(Guid ownerId)
    {
        var tenancy = new Tenancy(
            Guid.NewGuid(), ownerId, CustomerId, Guid.NewGuid(), 350_000_00, PropertyLeaseType.Rent)
        {
            Id = TenancyId,
            Status = TenancyStatus.AwaitingPayment,
        };

        _unitOfWork.Setup(u => u.TenancyQueries.GetByIdAsync(TenancyId)).ReturnsAsync(tenancy);
        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new Customer("Ada", "Obi", "ada@example.com", "0800", CustomerType.HouseOwner, "h"));
        _unitOfWork.Setup(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>())).ReturnsAsync(true);

        return tenancy;
    }

    /// <summary>
    /// The only event in the whole rail the owner has no reason to be watching for.
    /// Everything before it they set in motion themselves.
    /// </summary>
    [Fact]
    public async Task WhenTheTenantPays_TheOwnerIsTold()
    {
        var ownerId = Guid.NewGuid();
        var payment = Setup(PaymentStatus.Pending, amountKobo: 569_500_00);
        payment.PlatformChargeKobo = 19_500_00;
        GatewaySays(GatewayTransactionStatus.Successful, amountKobo: 569_500_00);
        SetupTenancy(ownerId);

        await _sut.ReconcileAsync(CustomerId, Reference);

        _unitOfWork.Verify(u => u.NotificationCommands.InsertAsync(
            It.Is<Notification>(n => n.RecipientId == ownerId
                && n.Type == NotificationType.TenancyPaid)), Times.Once);
    }

    /// <summary>
    /// What reached them, not what the tenant was charged. The difference is the
    /// part we kept, and reporting the total would overstate their bank balance by
    /// exactly that much.
    /// </summary>
    [Fact]
    public async Task TheOwnerIsToldWhatReachedThem_NotWhatWasCharged()
    {
        var payment = Setup(PaymentStatus.Pending, amountKobo: 569_500_00);
        payment.PlatformChargeKobo = 19_500_00;
        GatewaySays(GatewayTransactionStatus.Successful, amountKobo: 569_500_00);
        SetupTenancy(Guid.NewGuid());

        await _sut.ReconcileAsync(CustomerId, Reference);

        // 569,500 charged less 19,500 kept is 550,000.
        _email.Verify(e => e.SendTenancyPaidToOwnerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            550_000_00, TenancyId), Times.Once);
    }

    /// <summary>A payment that did not settle is not news for anybody.</summary>
    [Fact]
    public async Task AFailedPayment_TellsTheOwnerNothing()
    {
        Setup(PaymentStatus.Pending);
        GatewaySays(GatewayTransactionStatus.Abandoned);
        SetupTenancy(Guid.NewGuid());

        await _sut.ReconcileAsync(CustomerId, Reference);

        _unitOfWork.Verify(u => u.NotificationCommands.InsertAsync(
            It.Is<Notification>(n => n.Type == NotificationType.TenancyPaid)), Times.Never);
    }
}
