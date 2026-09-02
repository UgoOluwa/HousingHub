using System.Text.Json;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Payments;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PaymentService;

/// <summary>
/// Collecting fees owed to Housing Hub.
/// </summary>
/// <remarks>
/// <para>
/// Three rules hold everywhere in here, and each of them is a class of bug that
/// payment integrations reliably ship with:
/// </para>
/// <list type="number">
/// <item>
/// <b>A client never supplies an amount.</b> Every figure is computed from the fee
/// catalogue at the moment the attempt is created, and re-checked against the
/// gateway before anything is handed over.
/// </item>
/// <item>
/// <b>A redirect is not evidence.</b> The payer controls the browser that returns
/// to the callback URL, so arriving there proves nothing. Only a signed webhook,
/// re-read from the gateway, settles a payment.
/// </item>
/// <item>
/// <b>Everything is idempotent.</b> Webhooks are retried by design. The state
/// machine on <see cref="Payment"/> makes a repeat delivery a no-op rather than a
/// second entitlement.
/// </item>
/// </list>
/// <para>
/// It deliberately does not depend on <c>IVerificationService</c>. Submitting a
/// case stays that service's single responsibility, and this one only answers
/// whether a subject has been paid for — which keeps the dependency one-way and
/// means there is exactly one code path that moves a case out of Draft.
/// </para>
/// </remarks>
public class PaymentService : IPaymentService
{
    /// <summary>
    /// How long an unfinished attempt is offered back before a new one is started.
    /// </summary>
    /// <remarks>
    /// Exists to stop a refreshed page becoming a second charge. Short enough that a
    /// payer who genuinely abandoned one and came back later gets a fresh attempt
    /// rather than a stale gateway page.
    /// </remarks>
    private static readonly TimeSpan PendingAttemptReuseWindow = TimeSpan.FromMinutes(15);

    private const string ReferenceIndex = "Reference-index";
    private const string SubjectIndex = "SubjectId-index";
    private const string CustomerIndex = "CustomerId-index";

    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IPaymentGateway _gateway;
    private readonly PaymentFeeCatalogue _fees;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IUnitOfWOrk unitOfWork,
        IPaymentGateway gateway,
        PaymentFeeCatalogue fees,
        IConfiguration configuration,
        ILogger<PaymentService> logger)
    {
        _unitOfWork = unitOfWork;
        _gateway = gateway;
        _fees = fees;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsPaymentRequired => _fees.IsEnabled;

    public async Task<BaseResponse<PaymentQuoteDto>> QuoteVerificationCaseAsync(Guid customerId, Guid caseId)
    {
        try
        {
            var (verificationCase, customer, error) = await LoadOwnedCaseAsync(customerId, caseId);
            if (verificationCase is null || customer is null) return Fail<PaymentQuoteDto>(error!);

            // Ownership is still checked before answering, even when nothing is
            // charged — the reply says whether this case needs paying for, which is
            // not a question a stranger should be able to ask about it.
            if (!_fees.IsEnabled)
            {
                return Ok(new PaymentQuoteDto(
                    PaymentPurpose.IdentityVerification, 0, 0, 0, _fees.Currency,
                    IncludesIdentityVerification: false,
                    IsAlreadyPaid: false,
                    IsPaymentRequired: false));
            }

            if (!TryPrice(verificationCase.SubjectType, customer, out var pricing, out var priceError))
                return Fail<PaymentQuoteDto>(priceError!);

            var alreadyPaid = await HasSettledPaymentAsync(caseId);

            return Ok(new PaymentQuoteDto(
                pricing.Purpose,
                pricing.PurposeFeeKobo,
                pricing.IdentityFeeKobo,
                pricing.PurposeFeeKobo + pricing.IdentityFeeKobo,
                _fees.Currency,
                pricing.IdentityFeeKobo > 0,
                alreadyPaid,
                IsPaymentRequired: true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error quoting verification case {CaseId}", caseId);
            return Fail<PaymentQuoteDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<PaymentDto>> InitialiseVerificationPaymentAsync(
        Guid customerId, Guid caseId, string? callbackUrl)
    {
        try
        {
            if (!_fees.IsEnabled)
                return Fail<PaymentDto>(ResponseMessages.PaymentsNotEnabled);

            var (verificationCase, customer, error) = await LoadOwnedCaseAsync(customerId, caseId);
            if (verificationCase is null || customer is null) return Fail<PaymentDto>(error!);

            // Only a draft is payable. A case already with review has been paid for,
            // and a decided one cannot be paid for again.
            if (verificationCase.Status != VerificationCaseStatus.Draft)
                return Fail<PaymentDto>(ResponseMessages.PaymentCaseNotPayable);

            if (await HasSettledPaymentAsync(caseId))
                return Fail<PaymentDto>(ResponseMessages.PaymentAlreadySettled);

            if (!TryPrice(verificationCase.SubjectType, customer, out var pricing, out var priceError))
                return Fail<PaymentDto>(priceError!);

            // Hand back an attempt already in flight rather than registering another.
            var reusable = await FindReusablePendingAttemptAsync(caseId, pricing);
            if (reusable is not null)
                return Ok(ToDto(reusable));

            var payment = new Payment(
                reference: $"HH-{Guid.NewGuid():N}",
                customerId: customerId,
                purpose: pricing.Purpose,
                subjectId: caseId,
                purposeFeeKobo: pricing.PurposeFeeKobo,
                identityFeeKobo: pricing.IdentityFeeKobo,
                currency: _fees.Currency,
                provider: _gateway.Name);

            // Persisted before the gateway is called, not after. A webhook can arrive
            // while the initialise response is still in flight, and it needs a row to
            // land on — otherwise a real payment is confirmed against a reference this
            // system has never heard of.
            if (!await _unitOfWork.PaymentCommands.InsertAsync(payment))
                return Fail<PaymentDto>(ResponseMessages.UnexpectedError);

            var initialisation = await _gateway.InitialiseAsync(new GatewayChargeRequest(
                Reference: payment.Reference,
                AmountKobo: payment.AmountKobo,
                CustomerEmail: customer.Email,
                Currency: payment.Currency,
                CallbackUrl: ResolveCallbackUrl(callbackUrl),
                Metadata: new Dictionary<string, string>
                {
                    ["purpose"] = pricing.Purpose.ToString(),
                    ["verificationCaseId"] = caseId.ToString(),
                    ["customerId"] = customerId.ToString(),
                }));

            if (!initialisation.IsSuccessful || string.IsNullOrWhiteSpace(initialisation.AuthorisationUrl))
            {
                payment.TryFail(PaymentStatus.Failed, initialisation.Error);
                await _unitOfWork.PaymentCommands.UpdateAsync(payment);
                await _unitOfWork.SaveAsync();
                return Fail<PaymentDto>(initialisation.Error ?? ResponseMessages.PaymentCouldNotStart);
            }

            payment.RecordInitialisation(initialisation.AuthorisationUrl, initialisation.ProviderReference);
            await _unitOfWork.PaymentCommands.UpdateAsync(payment);
            await _unitOfWork.SaveAsync();

            return Ok(ToDto(payment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initialising payment for verification case {CaseId}", caseId);
            return Fail<PaymentDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<bool> IsSubjectPaidForAsync(Guid subjectId)
    {
        // Not "no payment found, so no". With charging switched off there is nothing
        // to find and nothing to pay, and every caller gating on this should proceed.
        if (!_fees.IsEnabled) return true;

        return await HasSettledPaymentAsync(subjectId);
    }

    public async Task<BaseResponse<PaymentDto>> GetByReferenceAsync(Guid customerId, string reference)
    {
        try
        {
            var payment = await FindByReferenceAsync(reference);

            // Same answer for "no such payment" and "somebody else's payment". A
            // distinguishable response would turn this into an oracle for guessing
            // references.
            if (payment is null || payment.CustomerId != customerId)
                return Fail<PaymentDto>(ResponseMessages.SetNotFoundMessage("payment"));

            return Ok(ToDto(payment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading payment {Reference}", reference);
            return Fail<PaymentDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<List<PaymentDto>>> GetMyPaymentsAsync(Guid customerId)
    {
        try
        {
            var payments = await _unitOfWork.PaymentQueries.QueryByIndexAsync(CustomerIndex, customerId);

            var dtos = payments
                .OrderByDescending(p => p.DateCreated)
                .Select(ToDto)
                .ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing payments for customer {CustomerId}", customerId);
            return new BaseResponse<List<PaymentDto>>(
                new List<PaymentDto>(), false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <inheritdoc />
    public async Task<bool> HandleWebhookAsync(
        string rawBody, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        // Authenticity first, before the body is parsed, let alone acted on. An
        // unsigned or wrongly signed body is discarded without being read.
        if (!_gateway.IsWebhookAuthentic(rawBody, signatureHeader))
        {
            _logger.LogWarning("Rejected a payment webhook with a missing or invalid signature");
            return false;
        }

        string? eventName;
        string? reference;

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            eventName = root.TryGetProperty("event", out var e) ? e.GetString() : null;
            reference = root.TryGetProperty("data", out var data) && data.TryGetProperty("reference", out var r)
                ? r.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            // Signed but unparseable. Retrying will not change the outcome, so accept
            // it and stop the provider redelivering forever.
            _logger.LogError(ex, "A signed payment webhook could not be parsed");
            return true;
        }

        if (!string.Equals(eventName, "charge.success", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Ignoring payment webhook event {Event}", eventName);
            return true;
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            _logger.LogError("A charge.success webhook arrived with no reference");
            return true;
        }

        var payment = await FindByReferenceAsync(reference);
        if (payment is null)
        {
            // Almost always a webhook for another environment pointed at this one —
            // dev and production share a Paystack account until they have separate
            // ones. Accepted so it is not redelivered indefinitely.
            _logger.LogWarning("Payment webhook for unknown reference {Reference}", reference);
            return true;
        }

        // Re-read from the gateway rather than trusting the amount in the payload.
        // The signature proves the body came from the provider; asking the provider
        // directly is what proves it is still true and was not a replay of an
        // earlier, smaller charge.
        var transaction = await _gateway.GetTransactionAsync(reference, cancellationToken);
        if (transaction is null)
        {
            _logger.LogError("Could not verify {Reference} with the gateway; asking for redelivery", reference);
            return false;
        }

        if (transaction.Status != GatewayTransactionStatus.Successful)
        {
            if (payment.TryFail(
                    transaction.Status == GatewayTransactionStatus.Abandoned
                        ? PaymentStatus.Abandoned
                        : PaymentStatus.Failed,
                    transaction.FailureReason))
            {
                await _unitOfWork.PaymentCommands.UpdateAsync(payment);
                await _unitOfWork.SaveAsync();
            }

            return true;
        }

        var outcome = payment.TrySettle(
            transaction.AmountKobo, transaction.ProviderReference, transaction.Channel);

        switch (outcome)
        {
            case PaymentSettlementOutcome.Settled:
                await _unitOfWork.PaymentCommands.UpdateAsync(payment);
                await _unitOfWork.SaveAsync();
                _logger.LogInformation(
                    "Settled payment {Reference} for {AmountKobo} kobo, purpose {Purpose}, subject {SubjectId}",
                    payment.Reference, payment.AmountKobo, payment.Purpose, payment.SubjectId);
                return true;

            case PaymentSettlementOutcome.AlreadySettled:
                // Expected, not exceptional: providers retry.
                return true;

            case PaymentSettlementOutcome.AmountMismatch:
                await _unitOfWork.PaymentCommands.UpdateAsync(payment);
                await _unitOfWork.SaveAsync();
                _logger.LogError(
                    "Payment {Reference} confirmed for {PaidKobo} kobo against an expected {ExpectedKobo}. Flagged; nothing handed over.",
                    payment.Reference, transaction.AmountKobo, payment.AmountKobo);
                return true;

            default:
                _logger.LogWarning(
                    "Payment {Reference} could not be settled from status {Status}", payment.Reference, payment.Status);
                return true;
        }
    }

    // ── internals ────────────────────────────────────────────────

    private sealed record Pricing(PaymentPurpose Purpose, long PurposeFeeKobo, long IdentityFeeKobo);

    /// <summary>
    /// Prices a case, bundling the identity fee when the payer does not already hold
    /// identity verification.
    /// </summary>
    /// <remarks>
    /// One rule on both sides of the marketplace: identity is bundled into the first
    /// paid verification you need, and never charged again. An agent pays for it with
    /// their business verification, an owner with their first property verification,
    /// and whoever gets there second pays only for the thing itself.
    /// </remarks>
    private bool TryPrice(
        VerificationSubjectType subjectType,
        Customer customer,
        out Pricing pricing,
        out string? error)
    {
        pricing = new Pricing(PaymentPurpose.IdentityVerification, 0, 0);

        var purpose = subjectType switch
        {
            VerificationSubjectType.Business => PaymentPurpose.BusinessVerification,
            VerificationSubjectType.Property => PaymentPurpose.PropertyVerification,
            VerificationSubjectType.Identity => PaymentPurpose.IdentityVerification,
            _ => (PaymentPurpose?)null,
        };

        if (purpose is null)
        {
            error = ResponseMessages.UnexpectedError;
            return false;
        }

        if (!_fees.TryGetFeeKobo(purpose.Value, out var purposeFee, out var feeError))
        {
            // The configuration problem goes to the log; the payer gets something
            // true that does not describe our deployment.
            _logger.LogError("Cannot price {Purpose}: {Error}", purpose, feeError);
            error = ResponseMessages.PaymentsNotConfigured;
            return false;
        }

        long identityFee = 0;

        // Not bundled onto itself, and not charged to somebody who already holds it.
        if (purpose != PaymentPurpose.IdentityVerification && !customer.IsKycVerified)
        {
            if (!_fees.TryGetFeeKobo(PaymentPurpose.IdentityVerification, out identityFee, out var identityError))
            {
                _logger.LogError("Cannot price the bundled identity check: {Error}", identityError);
                error = ResponseMessages.PaymentsNotConfigured;
                return false;
            }
        }

        pricing = new Pricing(purpose.Value, purposeFee, identityFee);
        error = null;
        return true;
    }

    /// <summary>
    /// Accepts a client-supplied return URL only if it points at a front end we
    /// already trust.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unchecked, this is an open redirect with a payment attached. The gateway sends
    /// the payer wherever this says once they have paid, so an attacker who can get
    /// someone to start a payment with their URL lands that person on a page of their
    /// choosing at the exact moment they are expecting a receipt — which is the most
    /// credible phishing context this product has.
    /// </para>
    /// <para>
    /// Validated against <c>Cors:AllowedOrigins</c> rather than a new setting: that
    /// list already answers "is this one of our front ends", it is required in
    /// production, and a second list would drift from it. A URL that does not match
    /// is dropped rather than rejected — the payment is still perfectly valid
    /// without a return URL, and the gateway shows its own receipt page.
    /// </para>
    /// </remarks>
    private string? ResolveCallbackUrl(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return null;

        if (!Uri.TryCreate(requested, UriKind.Absolute, out var candidate))
        {
            _logger.LogWarning("Discarded a payment callback URL that is not absolute");
            return null;
        }

        if (candidate.Scheme != Uri.UriSchemeHttps && !candidate.IsLoopback)
        {
            _logger.LogWarning("Discarded a non-HTTPS payment callback URL");
            return null;
        }

        var allowed = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        foreach (var origin in allowed)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var trusted)) continue;

            // Scheme, host and port must all match. Comparing the string prefix
            // instead would let "https://housinghub.ng.attacker.example" through.
            if (candidate.Scheme == trusted.Scheme
                && string.Equals(candidate.Host, trusted.Host, StringComparison.OrdinalIgnoreCase)
                && candidate.Port == trusted.Port)
            {
                return requested;
            }
        }

        _logger.LogWarning(
            "Discarded a payment callback URL for untrusted origin {Origin}", candidate.GetLeftPart(UriPartial.Authority));
        return null;
    }

    private async Task<(VerificationCase? Case, Customer? Customer, string? Error)> LoadOwnedCaseAsync(
        Guid customerId, Guid caseId)
    {
        var verificationCase = await _unitOfWork.VerificationCaseQueries.GetByIdAsync(caseId);

        // Ownership is re-checked here rather than trusted from the caller, and the
        // not-found and not-yours answers are the same one.
        if (verificationCase is null || verificationCase.SubmittedByCustomerId != customerId)
            return (null, null, ResponseMessages.SetNotFoundMessage("verification request"));

        var customer = await _unitOfWork.CustomerQueries.GetByIdAsync(customerId);
        if (customer is null)
            return (null, null, ResponseMessages.SetNotFoundMessage("customer"));

        return (verificationCase, customer, null);
    }

    private async Task<Payment?> FindByReferenceAsync(string reference)
    {
        var matches = await _unitOfWork.PaymentQueries.QueryByIndexAsync(ReferenceIndex, reference);
        return matches.FirstOrDefault();
    }

    private async Task<bool> HasSettledPaymentAsync(Guid subjectId)
    {
        var payments = await _unitOfWork.PaymentQueries.QueryByIndexAsync(SubjectIndex, subjectId);
        return payments.Any(p => p.Status == PaymentStatus.Successful);
    }

    /// <summary>
    /// A pending attempt for the same subject, at the same price, still inside the
    /// reuse window and with somewhere to send the payer.
    /// </summary>
    /// <remarks>
    /// The price is part of the match on purpose. If the fee changed since the
    /// attempt was created, the stale attempt would charge the old amount and then
    /// fail the amount check on settlement — so a repriced case starts fresh.
    /// </remarks>
    private async Task<Payment?> FindReusablePendingAttemptAsync(Guid subjectId, Pricing pricing)
    {
        var payments = await _unitOfWork.PaymentQueries.QueryByIndexAsync(SubjectIndex, subjectId);
        var cutoff = DateTime.UtcNow - PendingAttemptReuseWindow;
        var expectedTotal = pricing.PurposeFeeKobo + pricing.IdentityFeeKobo;

        return payments
            .Where(p => p.Status == PaymentStatus.Pending)
            .Where(p => p.AmountKobo == expectedTotal)
            .Where(p => !string.IsNullOrWhiteSpace(p.AuthorisationUrl))
            .Where(p => p.DateCreated >= cutoff)
            .OrderByDescending(p => p.DateCreated)
            .FirstOrDefault();
    }

    private static PaymentDto ToDto(Payment payment) => new(
        payment.Id,
        payment.Reference,
        payment.Purpose,
        payment.SubjectId,
        payment.AmountKobo,
        payment.PurposeFeeKobo,
        payment.IdentityFeeKobo,
        payment.IncludesIdentityVerification,
        payment.Currency,
        payment.Status,
        payment.Channel,
        payment.PaidAt,
        payment.DateCreated,
        // Only offered while the attempt is still payable — a stale gateway link on a
        // settled or failed payment is an invitation to pay twice.
        payment.Status == PaymentStatus.Pending ? payment.AuthorisationUrl : null);

    private static BaseResponse<T> Ok<T>(T data) =>
        new(data, true, string.Empty, ResponseMessages.Successful);

    private static BaseResponse<T> Fail<T>(string message) =>
        new(default, false, string.Empty, message);
}
