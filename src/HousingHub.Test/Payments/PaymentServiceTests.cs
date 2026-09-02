using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentServiceImpl = HousingHub.Service.PaymentService.PaymentService;

namespace HousingHub.Test.Payments;

/// <summary>
/// The orchestration around a payment: what is charged, what settles it, and what
/// must not.
/// </summary>
public class PaymentServiceTests
{
    private const long BusinessFee = 500_000;   // ₦5,000
    private const long IdentityFee = 250_000;   // ₦2,500
    private const string TrustedOrigin = "https://housinghub.example";

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid CaseId = Guid.NewGuid();

    private readonly Mock<IUnitOfWOrk> _unitOfWork;
    private readonly Mock<IPaymentGateway> _gateway;
    private readonly Mock<IEmailService> _email = new();
    private readonly List<Payment> _inserted = [];
    private readonly List<Payment> _updated = [];

    public PaymentServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _gateway = new Mock<IPaymentGateway>();
        _gateway.SetupGet(g => g.Name).Returns("paystack");

        _unitOfWork
            .Setup(u => u.PaymentCommands.InsertAsync(It.IsAny<Payment>()))
            .Callback<Payment>(_inserted.Add)
            .ReturnsAsync(true);
        _unitOfWork
            .Setup(u => u.PaymentCommands.UpdateAsync(It.IsAny<Payment>()))
            .Callback<Payment>(_updated.Add)
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        // No payments on any index unless a test says otherwise.
        _unitOfWork
            .Setup(u => u.PaymentQueries.QueryByIndexAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new List<Payment>());

        // Settling sends a receipt, which reads the payer. Present by default so
        // every webhook test does not have to arrange it.
        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(CustomerId)).ReturnsAsync(
            new Customer("Jane", "Doe", "jane@test.com", "08000000000", CustomerType.HouseOwner, "hash")
            {
                Id = CustomerId,
            });
    }

    private PaymentServiceImpl CreateSut(
        bool paymentsEnabled = true,
        long? businessFee = BusinessFee,
        long? identityFee = IdentityFee)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Payments:Enabled"] = paymentsEnabled ? "true" : "false",
            ["Payments:Currency"] = "NGN",
            ["Cors:AllowedOrigins:0"] = TrustedOrigin,
        };

        if (businessFee.HasValue)
            settings["Payments:Fees:BusinessVerification"] = businessFee.Value.ToString();
        if (identityFee.HasValue)
            settings["Payments:Fees:IdentityVerification"] = identityFee.Value.ToString();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new PaymentServiceImpl(
            _unitOfWork.Object,
            _gateway.Object,
            new PaymentFeeCatalogue(configuration),
            _email.Object,
            configuration,
            NullLogger<PaymentServiceImpl>.Instance);
    }

    // ── fixtures ─────────────────────────────────────────────────

    private void GivenDraftCaseOwnedByCustomer(bool isKycVerified = false)
    {
        var verificationCase = new VerificationCase(
            CustomerId, VerificationSubjectType.Business, CustomerId, VerificationTier.BusinessVerified)
        {
            Id = CaseId,
        };

        _unitOfWork.Setup(u => u.VerificationCaseQueries.GetByIdAsync(CaseId)).ReturnsAsync(verificationCase);
        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(CustomerId)).ReturnsAsync(
            new Customer("Jane", "Doe", "jane@test.com", "08000000000", CustomerType.HouseOwner, "hash")
            {
                Id = CustomerId,
                IsKycVerified = isKycVerified,
            });
    }

    private void GivenGatewayInitialisesSuccessfully() =>
        _gateway
            .Setup(g => g.InitialiseAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GatewayInitialisation.Succeeded($"{TrustedOrigin}/pay", "pstk_init"));

    private Payment GivenExistingPayment(
        PaymentStatus status,
        string reference = "HH-existing",
        long purposeFee = BusinessFee,
        long identityFee = IdentityFee,
        string? authorisationUrl = "https://checkout.paystack.com/x",
        DateTime? createdAt = null)
    {
        var payment = new Payment(
            reference, CustomerId, PaymentPurpose.BusinessVerification, CaseId,
            purposeFee, identityFee, "NGN", "paystack")
        {
            Status = status,
            AuthorisationUrl = authorisationUrl,
        };

        if (createdAt.HasValue) payment.DateCreated = createdAt.Value;

        _unitOfWork
            .Setup(u => u.PaymentQueries.QueryByIndexAsync("SubjectId-index", It.IsAny<object>()))
            .ReturnsAsync(new List<Payment> { payment });
        _unitOfWork
            .Setup(u => u.PaymentQueries.QueryByIndexAsync("Reference-index", It.IsAny<object>()))
            .ReturnsAsync(new List<Payment> { payment });

        return payment;
    }

    // Concatenated rather than an interpolated raw string: the JSON's own closing
    // braces collide with the interpolation delimiters.
    private static string WebhookBody(string reference, string @event = "charge.success") =>
        "{\"event\":\"" + @event + "\",\"data\":{\"reference\":\"" + reference + "\",\"amount\":750000}}";

    // ── the payment gate ─────────────────────────────────────────

    /// <summary>
    /// With charging switched off there is nothing to find and nothing owed, so the
    /// gate must pass. Reading "no payment row" as "not paid" would make turning the
    /// feature flag on retrospectively block every existing draft.
    /// </summary>
    [Fact]
    public async Task IsSubjectPaidForAsync_WhenChargingIsOff_IsTrue()
    {
        var sut = CreateSut(paymentsEnabled: false);

        Assert.True(await sut.IsSubjectPaidForAsync(CaseId));

        _unitOfWork.Verify(
            u => u.PaymentQueries.QueryByIndexAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task IsSubjectPaidForAsync_WithNoSettledPayment_IsFalse()
    {
        GivenExistingPayment(PaymentStatus.Pending);

        Assert.False(await CreateSut().IsSubjectPaidForAsync(CaseId));
    }

    [Fact]
    public async Task IsSubjectPaidForAsync_WithASettledPayment_IsTrue()
    {
        GivenExistingPayment(PaymentStatus.Successful);

        Assert.True(await CreateSut().IsSubjectPaidForAsync(CaseId));
    }

    /// <summary>A flagged payment is not a paid one. Money may have moved; nothing is owed to the payer.</summary>
    [Fact]
    public async Task IsSubjectPaidForAsync_WithAFlaggedPayment_IsFalse()
    {
        GivenExistingPayment(PaymentStatus.Flagged);

        Assert.False(await CreateSut().IsSubjectPaidForAsync(CaseId));
    }

    // ── pricing ──────────────────────────────────────────────────

    /// <summary>Identity is bundled into the first paid verification the payer needs.</summary>
    [Fact]
    public async Task Initialise_BundlesTheIdentityFee_WhenIdentityIsNotYetHeld()
    {
        GivenDraftCaseOwnedByCustomer(isKycVerified: false);
        GivenGatewayInitialisesSuccessfully();

        var result = await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.True(result.IsSuccessful);
        Assert.Equal(BusinessFee + IdentityFee, result.Data!.AmountKobo);
        Assert.Equal(IdentityFee, result.Data.IdentityFeeKobo);
        Assert.True(result.Data.IncludesIdentityVerification);
    }

    /// <summary>And never charged again once held.</summary>
    [Fact]
    public async Task Initialise_OmitsTheIdentityFee_WhenIdentityIsAlreadyHeld()
    {
        GivenDraftCaseOwnedByCustomer(isKycVerified: true);
        GivenGatewayInitialisesSuccessfully();

        var result = await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.True(result.IsSuccessful);
        Assert.Equal(BusinessFee, result.Data!.AmountKobo);
        Assert.Equal(0, result.Data.IdentityFeeKobo);
        Assert.False(result.Data.IncludesIdentityVerification);
    }

    /// <summary>
    /// A missing price is an error, never a free item. Defaulting to zero would give
    /// away the thing it was supposed to price, silently.
    /// </summary>
    [Fact]
    public async Task Initialise_WithNoConfiguredPrice_RefusesAndChargesNothing()
    {
        GivenDraftCaseOwnedByCustomer();

        var result = await CreateSut(businessFee: null)
            .InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PaymentsNotConfigured, result.Message);
        Assert.Empty(_inserted);
        _gateway.Verify(
            g => g.InitialiseAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The bundled identity fee is priced too, or the bundle is incomplete.</summary>
    [Fact]
    public async Task Initialise_WithNoConfiguredIdentityPrice_RefusesWhenTheBundleNeedsIt()
    {
        GivenDraftCaseOwnedByCustomer(isKycVerified: false);

        var result = await CreateSut(identityFee: null)
            .InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.False(result.IsSuccessful);
        Assert.Empty(_inserted);
    }

    // ── initialising ─────────────────────────────────────────────

    [Fact]
    public async Task Initialise_WhenChargingIsOff_Refuses()
    {
        GivenDraftCaseOwnedByCustomer();

        var result = await CreateSut(paymentsEnabled: false)
            .InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PaymentsNotEnabled, result.Message);
    }

    /// <summary>Someone else's case is indistinguishable from one that does not exist.</summary>
    [Fact]
    public async Task Initialise_ForSomebodyElsesCase_ReportsNotFound()
    {
        GivenDraftCaseOwnedByCustomer();

        var result = await CreateSut()
            .InitialiseVerificationPaymentAsync(Guid.NewGuid(), CaseId, null);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.SetNotFoundMessage("verification request"), result.Message);
        Assert.Empty(_inserted);
    }

    [Fact]
    public async Task Initialise_WhenAlreadyPaid_RefusesRatherThanChargingTwice()
    {
        GivenDraftCaseOwnedByCustomer();
        GivenExistingPayment(PaymentStatus.Successful);

        var result = await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PaymentAlreadySettled, result.Message);
        Assert.Empty(_inserted);
    }

    /// <summary>A double-clicked button is not a double charge.</summary>
    [Fact]
    public async Task Initialise_Twice_ReusesTheAttemptAlreadyInFlight()
    {
        GivenDraftCaseOwnedByCustomer();
        var existing = GivenExistingPayment(PaymentStatus.Pending, createdAt: DateTime.UtcNow);

        var result = await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.True(result.IsSuccessful);
        Assert.Equal(existing.Reference, result.Data!.Reference);
        Assert.Empty(_inserted);
        _gateway.Verify(
            g => g.InitialiseAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A stale attempt is not reused — its gateway page has long since expired.</summary>
    [Fact]
    public async Task Initialise_DoesNotReuseAnOldPendingAttempt()
    {
        GivenDraftCaseOwnedByCustomer();
        GivenExistingPayment(PaymentStatus.Pending, createdAt: DateTime.UtcNow.AddHours(-2));
        GivenGatewayInitialisesSuccessfully();

        var result = await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.True(result.IsSuccessful);
        Assert.Single(_inserted);
    }

    /// <summary>
    /// A repriced case starts fresh. Reusing an attempt created at the old price
    /// would charge the old amount and then fail the amount check on settlement.
    /// </summary>
    [Fact]
    public async Task Initialise_DoesNotReuseAnAttemptAtADifferentPrice()
    {
        GivenDraftCaseOwnedByCustomer();
        GivenExistingPayment(PaymentStatus.Pending, purposeFee: 1, createdAt: DateTime.UtcNow);
        GivenGatewayInitialisesSuccessfully();

        var result = await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.True(result.IsSuccessful);
        Assert.Single(_inserted);
    }

    [Fact]
    public async Task Initialise_WhenTheCaseIsNoLongerADraft_Refuses()
    {
        var submitted = new VerificationCase(
            CustomerId, VerificationSubjectType.Business, CustomerId, VerificationTier.BusinessVerified)
        {
            Id = CaseId,
            Status = VerificationCaseStatus.UnderReview,
        };
        _unitOfWork.Setup(u => u.VerificationCaseQueries.GetByIdAsync(CaseId)).ReturnsAsync(submitted);
        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(CustomerId)).ReturnsAsync(
            new Customer("Jane", "Doe", "jane@test.com", "08000000000", CustomerType.HouseOwner, "hash")
            {
                Id = CustomerId,
            });

        var result = await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PaymentCaseNotPayable, result.Message);
    }

    /// <summary>The amount sent to the gateway is the amount recorded, in kobo.</summary>
    [Fact]
    public async Task Initialise_SendsTheServerComputedAmountInKobo()
    {
        GivenDraftCaseOwnedByCustomer();
        GatewayChargeRequest? captured = null;
        _gateway
            .Setup(g => g.InitialiseAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GatewayChargeRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(GatewayInitialisation.Succeeded($"{TrustedOrigin}/pay", "pstk_init"));

        await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.NotNull(captured);
        Assert.Equal(BusinessFee + IdentityFee, captured!.AmountKobo);
        Assert.Equal("NGN", captured.Currency);
        Assert.Equal("jane@test.com", captured.CustomerEmail);
    }

    /// <summary>
    /// A failed initialisation does not leave a payable row behind — otherwise a
    /// later webhook could settle an attempt the payer never saw.
    /// </summary>
    [Fact]
    public async Task Initialise_WhenTheGatewayRefuses_MarksTheAttemptFailed()
    {
        GivenDraftCaseOwnedByCustomer();
        _gateway
            .Setup(g => g.InitialiseAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GatewayInitialisation.Failed("nope"));

        var result = await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, null);

        Assert.False(result.IsSuccessful);
        var failed = Assert.Single(_updated);
        Assert.Equal(PaymentStatus.Failed, failed.Status);
    }

    // ── the callback URL, as an open redirect ────────────────────

    [Fact]
    public async Task Initialise_KeepsACallbackUrlOnATrustedOrigin()
    {
        GivenDraftCaseOwnedByCustomer();
        GatewayChargeRequest? captured = null;
        _gateway
            .Setup(g => g.InitialiseAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GatewayChargeRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(GatewayInitialisation.Succeeded($"{TrustedOrigin}/pay", "pstk_init"));

        await CreateSut().InitialiseVerificationPaymentAsync(
            CustomerId, CaseId, $"{TrustedOrigin}/verification/done");

        Assert.Equal($"{TrustedOrigin}/verification/done", captured!.CallbackUrl);
    }

    /// <summary>
    /// The gateway sends the payer wherever this points once they have paid, so an
    /// unchecked value is an open redirect at the most credible possible moment.
    /// </summary>
    [Theory]
    [InlineData("https://attacker.example/receipt")]
    // Prefix-matching the trusted host rather than equalling it.
    [InlineData("https://housinghub.example.attacker.example/receipt")]
    // Same host, different scheme.
    [InlineData("http://housinghub.example/receipt")]
    // Same host, different port.
    [InlineData("https://housinghub.example:8443/receipt")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative/path")]
    public async Task Initialise_DropsACallbackUrlOnAnUntrustedOrigin(string callbackUrl)
    {
        GivenDraftCaseOwnedByCustomer();
        GatewayChargeRequest? captured = null;
        _gateway
            .Setup(g => g.InitialiseAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GatewayChargeRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(GatewayInitialisation.Succeeded($"{TrustedOrigin}/pay", "pstk_init"));

        var result = await CreateSut().InitialiseVerificationPaymentAsync(CustomerId, CaseId, callbackUrl);

        // Dropped, not rejected: the payment is valid without a return URL.
        Assert.True(result.IsSuccessful);
        Assert.Null(captured!.CallbackUrl);
    }

    // ── webhooks ─────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_WithAnInvalidSignature_IsRejectedWithoutBeingRead()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        _gateway.Setup(g => g.IsWebhookAuthentic(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var handled = await CreateSut().HandleWebhookAsync(WebhookBody(payment.Reference), "forged");

        Assert.False(handled);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Empty(_updated);
        _gateway.Verify(
            g => g.GetTransactionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_ForASuccessfulCharge_Settles()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();
        GivenGatewayReports(payment.Reference, GatewayTransactionStatus.Successful, payment.AmountKobo);

        var handled = await CreateSut().HandleWebhookAsync(WebhookBody(payment.Reference), "sig");

        Assert.True(handled);
        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Equal("card", payment.Channel);
        Assert.Single(_updated);
    }

    /// <summary>
    /// The provider is asked directly rather than believed. A signature proves the
    /// body came from them; re-reading proves it is still true and not a replay of
    /// an earlier, smaller charge.
    /// </summary>
    [Fact]
    public async Task Webhook_ReVerifiesWithTheGateway_RatherThanTrustingThePayload()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();
        GivenGatewayReports(payment.Reference, GatewayTransactionStatus.Successful, payment.AmountKobo);

        await CreateSut().HandleWebhookAsync(WebhookBody(payment.Reference), "sig");

        _gateway.Verify(
            g => g.GetTransactionAsync(payment.Reference, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Redelivery is normal traffic, and must settle exactly once.</summary>
    [Fact]
    public async Task Webhook_DeliveredTwice_SettlesOnce()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();
        GivenGatewayReports(payment.Reference, GatewayTransactionStatus.Successful, payment.AmountKobo);
        var sut = CreateSut();

        Assert.True(await sut.HandleWebhookAsync(WebhookBody(payment.Reference), "sig"));
        var settledAt = payment.PaidAt;

        Assert.True(await sut.HandleWebhookAsync(WebhookBody(payment.Reference), "sig"));

        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Equal(settledAt, payment.PaidAt);
        Assert.Single(_updated);
    }

    /// <summary>
    /// A genuinely signed webhook confirming the wrong amount hands over nothing.
    /// </summary>
    [Fact]
    public async Task Webhook_ConfirmingTheWrongAmount_FlagsAndGrantsNothing()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();
        GivenGatewayReports(payment.Reference, GatewayTransactionStatus.Successful, amountKobo: 100);

        var handled = await CreateSut().HandleWebhookAsync(WebhookBody(payment.Reference), "sig");

        // Accepted so it is not redelivered forever — a person has to look at it.
        Assert.True(handled);
        Assert.Equal(PaymentStatus.Flagged, payment.Status);
        Assert.False(payment.IsSettled);
    }

    /// <summary>
    /// Almost always another environment's webhook pointed at this one. Accepted, so
    /// the provider stops redelivering something we will never recognise.
    /// </summary>
    [Fact]
    public async Task Webhook_ForAnUnknownReference_IsAcceptedAndIgnored()
    {
        GivenAuthenticWebhook();

        var handled = await CreateSut().HandleWebhookAsync(WebhookBody("HH-never-seen"), "sig");

        Assert.True(handled);
        _gateway.Verify(
            g => g.GetTransactionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Our own dependency failing must ask for redelivery. Returning success here
    /// would drop a real payment on the floor.
    /// </summary>
    [Fact]
    public async Task Webhook_WhenTheGatewayCannotBeReached_AsksForRedelivery()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();
        _gateway
            .Setup(g => g.GetTransactionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GatewayTransaction?)null);

        var handled = await CreateSut().HandleWebhookAsync(WebhookBody(payment.Reference), "sig");

        Assert.False(handled);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task Webhook_ForAFailedCharge_RecordsTheFailure()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();
        GivenGatewayReports(payment.Reference, GatewayTransactionStatus.Failed, payment.AmountKobo);

        var handled = await CreateSut().HandleWebhookAsync(WebhookBody(payment.Reference), "sig");

        Assert.True(handled);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
    }

    [Fact]
    public async Task Webhook_ForAnEventWeDoNotHandle_IsAcceptedAndIgnored()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();

        var handled = await CreateSut()
            .HandleWebhookAsync(WebhookBody(payment.Reference, "transfer.success"), "sig");

        Assert.True(handled);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Empty(_updated);
    }

    /// <summary>Signed but unparseable. Retrying cannot help, so it is accepted rather than looped on.</summary>
    [Fact]
    public async Task Webhook_WithAnUnparseableBody_IsAcceptedRatherThanRetriedForever()
    {
        GivenAuthenticWebhook();

        Assert.True(await CreateSut().HandleWebhookAsync("{not json", "sig"));
    }

    [Fact]
    public async Task Webhook_WithNoReference_IsAcceptedAndIgnored()
    {
        GivenAuthenticWebhook();

        Assert.True(await CreateSut().HandleWebhookAsync("""{"event":"charge.success","data":{}}""", "sig"));
    }

    // ── reading payments back ────────────────────────────────────

    [Fact]
    public async Task GetByReference_DoesNotRevealSomebodyElsesPayment()
    {
        GivenExistingPayment(PaymentStatus.Successful);

        var result = await CreateSut().GetByReferenceAsync(Guid.NewGuid(), "HH-existing");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.SetNotFoundMessage("payment"), result.Message);
        Assert.Null(result.Data);
    }

    /// <summary>
    /// A settled payment carries no gateway link. Offering one is an invitation to
    /// pay for the same thing twice.
    /// </summary>
    [Fact]
    public async Task GetByReference_WithholdsTheGatewayLink_OnceSettled()
    {
        GivenExistingPayment(PaymentStatus.Successful);

        var result = await CreateSut().GetByReferenceAsync(CustomerId, "HH-existing");

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Data!.AuthorisationUrl);
    }

    [Fact]
    public async Task GetByReference_OffersTheGatewayLink_WhilePending()
    {
        GivenExistingPayment(PaymentStatus.Pending);

        var result = await CreateSut().GetByReferenceAsync(CustomerId, "HH-existing");

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.AuthorisationUrl);
    }

    // ── helpers ──────────────────────────────────────────────────

    private void GivenAuthenticWebhook() =>
        _gateway.Setup(g => g.IsWebhookAuthentic(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

    private void GivenGatewayReports(string reference, GatewayTransactionStatus status, long amountKobo) =>
        _gateway
            .Setup(g => g.GetTransactionAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayTransaction(reference, status, amountKobo, "pstk_1", "card", null));

    // ── quoting ──────────────────────────────────────────────────

    /// <summary>
    /// With charging off the quote says so plainly, rather than failing and leaving
    /// the client to guess whether that meant "free" or "broken".
    /// </summary>
    [Fact]
    public async Task Quote_WhenChargingIsOff_SaysNoPaymentIsRequired()
    {
        GivenDraftCaseOwnedByCustomer();

        var result = await CreateSut(paymentsEnabled: false)
            .QuoteVerificationCaseAsync(CustomerId, CaseId);

        Assert.True(result.IsSuccessful);
        Assert.False(result.Data!.IsPaymentRequired);
        Assert.Equal(0, result.Data.TotalKobo);
    }

    /// <summary>Ownership is checked even when nothing is charged.</summary>
    [Fact]
    public async Task Quote_ForSomebodyElsesCase_IsRefused_EvenWhenChargingIsOff()
    {
        GivenDraftCaseOwnedByCustomer();

        var result = await CreateSut(paymentsEnabled: false)
            .QuoteVerificationCaseAsync(Guid.NewGuid(), CaseId);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task Quote_ShowsTheBundleBrokenDown()
    {
        GivenDraftCaseOwnedByCustomer(isKycVerified: false);

        var result = await CreateSut().QuoteVerificationCaseAsync(CustomerId, CaseId);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.IsPaymentRequired);
        Assert.Equal(BusinessFee, result.Data.PurposeFeeKobo);
        Assert.Equal(IdentityFee, result.Data.IdentityFeeKobo);
        Assert.Equal(BusinessFee + IdentityFee, result.Data.TotalKobo);
        Assert.False(result.Data.IsAlreadyPaid);
    }

    [Fact]
    public async Task Quote_ReportsWhenItHasAlreadyBeenPaid()
    {
        GivenDraftCaseOwnedByCustomer();
        GivenExistingPayment(PaymentStatus.Successful);

        var result = await CreateSut().QuoteVerificationCaseAsync(CustomerId, CaseId);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.IsAlreadyPaid);
    }

    // ── receipts ─────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_OnSettlement_SendsAReceipt()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();
        GivenGatewayReports(payment.Reference, GatewayTransactionStatus.Successful, payment.AmountKobo);

        await CreateSut().HandleWebhookAsync(WebhookBody(payment.Reference), "sig");

        _email.Verify(e => e.SendPaymentReceiptAsync(
            "jane@test.com", "Jane", payment.Reference,
            It.IsAny<string>(), payment.AmountKobo, IdentityFee, "card"), Times.Once);
    }

    /// <summary>Redelivery must not send a second receipt.</summary>
    [Fact]
    public async Task Webhook_DeliveredTwice_SendsOneReceipt()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();
        GivenGatewayReports(payment.Reference, GatewayTransactionStatus.Successful, payment.AmountKobo);
        var sut = CreateSut();

        await sut.HandleWebhookAsync(WebhookBody(payment.Reference), "sig");
        await sut.HandleWebhookAsync(WebhookBody(payment.Reference), "sig");

        _email.Verify(e => e.SendPaymentReceiptAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// A flagged payment hands nothing over, so it must not produce a receipt saying
    /// the request is with the review team.
    /// </summary>
    [Fact]
    public async Task Webhook_ConfirmingTheWrongAmount_SendsNoReceipt()
    {
        var payment = GivenExistingPayment(PaymentStatus.Pending);
        GivenAuthenticWebhook();
        GivenGatewayReports(payment.Reference, GatewayTransactionStatus.Successful, amountKobo: 100);

        await CreateSut().HandleWebhookAsync(WebhookBody(payment.Reference), "sig");

        _email.Verify(e => e.SendPaymentReceiptAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    // ── refund webhooks ──────────────────────────────────────────

    /// <summary>Refund events name the charge in `transaction_reference`, not `reference`.</summary>
    private static string RefundWebhookBody(string transactionReference, string @event, long amount = 750000) =>
        "{\"event\":\"" + @event + "\",\"data\":{\"id\":991,\"transaction_reference\":\""
        + transactionReference + "\",\"reference\":\"refund_ref\",\"amount\":" + amount + "}}";

    [Fact]
    public async Task Webhook_ForAProcessedRefund_RecordsItAndTellsThePayer()
    {
        var payment = GivenExistingPayment(PaymentStatus.RefundPending);
        payment.RefundReason = "Duplicate submission, refunded in full";
        GivenAuthenticWebhook();

        var handled = await CreateSut()
            .HandleWebhookAsync(RefundWebhookBody(payment.Reference, "refund.processed"), "sig");

        Assert.True(handled);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.NotNull(payment.RefundedAt);

        _email.Verify(e => e.SendPaymentRefundedAsync(
            "jane@test.com", "Jane", payment.Reference, 750000,
            "Duplicate submission, refunded in full"), Times.Once);
    }

    /// <summary>
    /// A refund event reads the charge's reference from the right field. Taking
    /// `reference` would look up the refund's own id and silently drop the event.
    /// </summary>
    [Fact]
    public async Task Webhook_ForARefund_ResolvesThePaymentByTransactionReference()
    {
        var payment = GivenExistingPayment(PaymentStatus.RefundPending);
        GivenAuthenticWebhook();

        await CreateSut().HandleWebhookAsync(RefundWebhookBody(payment.Reference, "refund.processed"), "sig");

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    /// <summary>Refund webhooks are retried too, and must not refund twice in our records.</summary>
    [Fact]
    public async Task Webhook_ForAProcessedRefund_DeliveredTwice_RecordsOnce()
    {
        var payment = GivenExistingPayment(PaymentStatus.RefundPending);
        GivenAuthenticWebhook();
        var sut = CreateSut();

        await sut.HandleWebhookAsync(RefundWebhookBody(payment.Reference, "refund.processed"), "sig");
        var refundedAt = payment.RefundedAt;
        await sut.HandleWebhookAsync(RefundWebhookBody(payment.Reference, "refund.processed"), "sig");

        Assert.Equal(refundedAt, payment.RefundedAt);
        _email.Verify(e => e.SendPaymentRefundedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// A failed refund goes back to where it was, so the payment is not stuck
    /// pending a refund that will never arrive — and the payer is not told anything
    /// went back when it did not.
    /// </summary>
    [Fact]
    public async Task Webhook_ForAFailedRefund_RestoresThePaymentAndSendsNothing()
    {
        var payment = GivenExistingPayment(PaymentStatus.RefundPending);
        GivenAuthenticWebhook();

        var handled = await CreateSut()
            .HandleWebhookAsync(RefundWebhookBody(payment.Reference, "refund.failed"), "sig");

        Assert.True(handled);
        Assert.Equal(PaymentStatus.Successful, payment.Status);

        _email.Verify(e => e.SendPaymentRefundedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_ForARefund_WithAnInvalidSignature_IsRejected()
    {
        var payment = GivenExistingPayment(PaymentStatus.RefundPending);
        _gateway.Setup(g => g.IsWebhookAuthentic(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var handled = await CreateSut()
            .HandleWebhookAsync(RefundWebhookBody(payment.Reference, "refund.processed"), "forged");

        Assert.False(handled);
        Assert.Equal(PaymentStatus.RefundPending, payment.Status);
    }

    /// <summary>A refunded payment no longer satisfies the verification gate.</summary>
    [Fact]
    public async Task IsSubjectPaidForAsync_WithARefundedPayment_IsFalse()
    {
        GivenExistingPayment(PaymentStatus.Refunded);

        Assert.False(await CreateSut().IsSubjectPaidForAsync(CaseId));
    }

    [Fact]
    public async Task IsSubjectPaidForAsync_WhileARefundIsInFlight_IsFalse()
    {
        GivenExistingPayment(PaymentStatus.RefundPending);

        Assert.False(await CreateSut().IsSubjectPaidForAsync(CaseId));
    }
}
