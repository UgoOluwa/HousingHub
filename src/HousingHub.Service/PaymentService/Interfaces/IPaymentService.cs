using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Payment;

namespace HousingHub.Service.PaymentService.Interfaces;

public interface IPaymentService
{
    /// <summary>Whether fees are being charged at all — see PaymentFeeCatalogue.IsEnabled.</summary>
    bool IsPaymentRequired { get; }

    /// <summary>What a verification case costs, and whether it has already been paid for.</summary>
    Task<BaseResponse<PaymentQuoteDto>> QuoteVerificationCaseAsync(Guid customerId, Guid caseId);

    /// <summary>
    /// Starts a payment for a verification case and returns where to send the payer.
    /// </summary>
    /// <remarks>
    /// Hands back the attempt already in flight if there is a recent one, rather than
    /// registering a second charge — the difference between a double-clicked button
    /// and a double payment.
    /// </remarks>
    Task<BaseResponse<PaymentDto>> InitialiseVerificationPaymentAsync(
        Guid customerId, Guid caseId, string? callbackUrl);

    /// <summary>
    /// Whether a settled payment exists for this subject.
    /// </summary>
    /// <remarks>
    /// Always true when charging is switched off, so every caller can gate on this
    /// one call without also having to know whether payments are enabled.
    /// </remarks>
    Task<bool> IsSubjectPaidForAsync(Guid subjectId);

    /// <summary>Reads one of the caller's own payments back, by our reference.</summary>
    Task<BaseResponse<PaymentDto>> GetByReferenceAsync(Guid customerId, string reference);

    /// <summary>Every payment the caller has made, newest first.</summary>
    Task<BaseResponse<List<PaymentDto>>> GetMyPaymentsAsync(Guid customerId);

    /// <summary>
    /// Processes a provider webhook.
    /// </summary>
    /// <remarks>
    /// Takes the raw body because the signature is computed over exact bytes. The
    /// return value is what the endpoint should tell the provider: false means "we
    /// could not process this, retry", which is why a transient failure must not
    /// return true.
    /// </remarks>
    Task<bool> HandleWebhookAsync(string rawBody, string? signatureHeader, CancellationToken cancellationToken = default);
}
