using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Payments;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PaymentService;

/// <summary>
/// Refunds.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately the only write path staff have over a payment. There is no endpoint
/// to mark one successful, unflag one, or edit an amount — each of those would be a
/// way to grant a paid service with no money moving, and the row it wrote would be
/// indistinguishable from the real thing.
/// </para>
/// <para>
/// A refund is different: it is verifiable against the provider afterwards, it moves
/// money in the direction that cannot enrich us, and there is no safe manual
/// alternative — telling an admin to use Paystack's dashboard leaves our own record
/// saying the customer still paid.
/// </para>
/// </remarks>
public class AdminPaymentCommandService : IAdminPaymentCommandService
{
    /// <summary>
    /// Shortest reason worth recording.
    /// </summary>
    /// <remarks>
    /// A refund with "n/a" or "asked" against it is unauditable six months later,
    /// when the question is why money left the account. Short enough not to be
    /// obstructive, long enough to force a sentence.
    /// </remarks>
    private const int MinimumReasonLength = 10;

    private const string ReferenceIndex = "Reference-index";

    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IPaymentGateway _gateway;
    private readonly IAdminPaymentQueryService _query;
    private readonly ILogger<AdminPaymentCommandService> _logger;

    public AdminPaymentCommandService(
        IUnitOfWOrk unitOfWork,
        IPaymentGateway gateway,
        IAdminPaymentQueryService query,
        ILogger<AdminPaymentCommandService> logger)
    {
        _unitOfWork = unitOfWork;
        _gateway = gateway;
        _query = query;
        _logger = logger;
    }

    public async Task<BaseResponse<AdminPaymentDto>> RefundAsync(string reference, string reason, Guid adminId)
    {
        try
        {
            if (adminId == Guid.Empty)
                return Fail(ResponseMessages.UnexpectedError);

            reason = reason?.Trim() ?? string.Empty;
            if (reason.Length < MinimumReasonLength)
                return Fail(ResponseMessages.RefundReasonRequired);

            var matches = await _unitOfWork.PaymentQueries.QueryByIndexAsync(ReferenceIndex, reference);
            var payment = matches.FirstOrDefault();

            if (payment is null)
                return Fail(ResponseMessages.SetNotFoundMessage("payment"));

            if (payment.Status is PaymentStatus.RefundPending or PaymentStatus.Refunded)
                return Fail(ResponseMessages.RefundAlreadyInProgress);

            if (!payment.IsRefundable)
                return Fail(ResponseMessages.RefundNotPossible);

            // Ask the provider what actually arrived rather than refunding what we
            // asked for. On a flagged payment those differ by definition, and that
            // is the case this feature mostly exists to resolve — refunding our own
            // figure would send back an amount nobody paid.
            var transaction = await _gateway.GetTransactionAsync(payment.Reference);

            if (transaction is null || transaction.Status != GatewayTransactionStatus.Successful)
            {
                _logger.LogWarning(
                    "Refused to refund {Reference}: the provider does not report it as a successful charge",
                    payment.Reference);
                return Fail(ResponseMessages.RefundNotConfirmedByProvider);
            }

            // Claim it before contacting the provider. Two admins clicking at once
            // would otherwise each see a refundable payment and each send a refund;
            // moving to RefundPending first means the second finds one in flight.
            var begun = payment.TryBeginRefund(transaction.AmountKobo, reason, adminId);
            if (begun != RefundOutcome.Requested)
                return Fail(ResponseMessages.RefundAlreadyInProgress);

            await _unitOfWork.PaymentCommands.UpdateAsync(payment);
            await _unitOfWork.SaveAsync();

            var refund = await _gateway.RefundAsync(payment.Reference, transaction.AmountKobo, reason);

            if (!refund.IsSuccessful)
            {
                // Put it back, so the payment does not sit forever pending a refund
                // that was never accepted. A flagged payment returns to the queue.
                payment.TryAbandonRefund(refund.Error);
                await _unitOfWork.PaymentCommands.UpdateAsync(payment);
                await _unitOfWork.SaveAsync();

                _logger.LogError(
                    "Refund of {Reference} was refused by the provider: {Error}", payment.Reference, refund.Error);
                return Fail(refund.Error ?? ResponseMessages.RefundNotPossible);
            }

            // Providers usually answer "pending" and confirm by webhook, in which
            // case this stays RefundPending and the webhook finishes it — including
            // sending the payer their confirmation.
            if (refund.IsComplete)
            {
                payment.TryCompleteRefund(refund.AmountKobo, refund.RefundReference);
                await _unitOfWork.PaymentCommands.UpdateAsync(payment);
                await _unitOfWork.SaveAsync();
            }

            _logger.LogInformation(
                "Refund of {AmountKobo} kobo for {Reference} {State} by admin {AdminId}. Reason: {Reason}",
                transaction.AmountKobo,
                payment.Reference,
                refund.IsComplete ? "completed" : "requested",
                adminId,
                reason);

            return await _query.GetByReferenceAsync(payment.Reference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding payment {Reference}", reference);
            return Fail(ResponseMessages.UnexpectedError);
        }
    }

    private static BaseResponse<AdminPaymentDto> Fail(string message) =>
        new(default, false, string.Empty, message);
}
