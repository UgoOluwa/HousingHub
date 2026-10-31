using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Payments;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.Dtos.Tenancy;
using HousingHub.Service.PaymentService.Interfaces;
using HousingHub.Service.TenancyService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.TenancyService;

/// <inheritdoc cref="ITenancyPaymentService"/>
public class TenancyPaymentService : ITenancyPaymentService
{
    private const string TenancyIndex = "TenancyId-index";
    private const string CustomerIndex = "CustomerId-index";

    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IPaymentGateway _gateway;
    private readonly TenancyChargeCalculator _calculator;
    private readonly PaymentFeeCatalogue _fees;
    private readonly ICallbackUrlResolver _callbacks;
    private readonly ILogger<TenancyPaymentService> _logger;

    public TenancyPaymentService(
        IUnitOfWOrk unitOfWork,
        IPaymentGateway gateway,
        TenancyChargeCalculator calculator,
        PaymentFeeCatalogue fees,
        ICallbackUrlResolver callbacks,
        ILogger<TenancyPaymentService> logger)
    {
        _unitOfWork = unitOfWork;
        _gateway = gateway;
        _calculator = calculator;
        _fees = fees;
        _callbacks = callbacks;
        _logger = logger;
    }

    public async Task<BaseResponse<TenancyPaymentQuoteDto>> QuoteAsync(Guid tenancyId, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await LoadForEitherPartyAsync(tenancyId, authenticatedUserId);
            if (tenancy is null) return Fail<TenancyPaymentQuoteDto>(ResponseMessages.SetNotFoundMessage("tenancy"));

            var fees = (await _unitOfWork.TenancyFeeQueries.QueryByIndexAsync(TenancyIndex, tenancyId))
                .OrderBy(f => f.DateCreated)
                .ToList();

            if (!_calculator.TryCalculate(tenancy.AgreedRentKobo, fees.Sum(f => f.AmountKobo),
                    out var charge, out var error))
                return Fail<TenancyPaymentQuoteDto>(error!);

            var payout = await FindPayoutAccountAsync(tenancy.LandlordCustomerId);

            return new BaseResponse<TenancyPaymentQuoteDto>(
                new TenancyPaymentQuoteDto(
                    tenancy.Id,
                    tenancy.Status,
                    charge.RentKobo,
                    charge.OwnerFeesKobo,
                    charge.CommissionKobo,
                    charge.ProcessingFeeKobo,
                    charge.TotalKobo,
                    _fees.Currency,
                    IsPayable: tenancy.Status is TenancyStatus.DocumentsAccepted or TenancyStatus.AwaitingPayment,
                    IsAlreadyPaid: await HasSettledPaymentAsync(tenancyId),
                    OwnerCanBePaid: payout is not null,
                    fees.Select(f => new TenancyFeeDto(f.Id, f.Name, f.Description, f.AmountKobo)).ToList()),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error quoting tenancy {TenancyId}", tenancyId);
            return Fail<TenancyPaymentQuoteDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<PaymentDto>> InitialiseAsync(
        Guid tenancyId, string? callbackUrl, Guid authenticatedUserId)
    {
        try
        {
            if (!_fees.IsEnabled) return Fail<PaymentDto>(ResponseMessages.PaymentsNotEnabled);

            var tenancy = await _unitOfWork.TenancyQueries.GetByIdAsync(tenancyId);

            // The tenant alone. The owner is a party to this tenancy and would
            // otherwise be able to start a charge against the other person's card.
            if (tenancy is null || tenancy.TenantCustomerId != authenticatedUserId)
                return Fail<PaymentDto>(ResponseMessages.SetNotFoundMessage("tenancy"));

            if (tenancy.Status is not (TenancyStatus.DocumentsAccepted or TenancyStatus.AwaitingPayment))
                return Fail<PaymentDto>(ResponseMessages.TenancyNotPayable);

            if (await HasSettledPaymentAsync(tenancyId))
                return Fail<PaymentDto>(ResponseMessages.PaymentAlreadySettled);

            // Checked before the tenant is sent anywhere. Starting a split with no
            // destination fails at the provider with a message about subaccounts,
            // which is nothing the person paying can act on.
            var payout = await FindPayoutAccountAsync(tenancy.LandlordCustomerId);
            if (payout is null)
            {
                await NotifyOwnerPayoutMissingAsync(tenancy);
                return Fail<PaymentDto>(ResponseMessages.PayoutAccountRequired);
            }

            var fees = await _unitOfWork.TenancyFeeQueries.QueryByIndexAsync(TenancyIndex, tenancyId);

            if (!_calculator.TryCalculate(tenancy.AgreedRentKobo, fees.Sum(f => f.AmountKobo),
                    out var charge, out var error))
                return Fail<PaymentDto>(error!);

            var tenant = await _unitOfWork.CustomerQueries.GetByIdAsync(tenancy.TenantCustomerId);
            if (tenant is null || string.IsNullOrWhiteSpace(tenant.Email))
                return Fail<PaymentDto>(ResponseMessages.UnexpectedError);

            var reusable = await FindReusablePendingAttemptAsync(tenancyId, charge.TotalKobo);
            if (reusable is not null)
                return new BaseResponse<PaymentDto>(ToDto(reusable), true, string.Empty, ResponseMessages.Successful);

            // The whole total sits on the purpose fee: a tenancy charge has no
            // identity component, and the breakdown lives on the tenancy, where the
            // rent and the fees are the record of what was agreed.
            var payment = new Payment(
                reference: $"HH-{Guid.NewGuid():N}",
                customerId: tenancy.TenantCustomerId,
                purpose: PaymentPurpose.TenancyRent,
                subjectId: tenancyId,
                purposeFeeKobo: charge.TotalKobo,
                identityFeeKobo: 0,
                currency: _fees.Currency,
                provider: _gateway.Name)
            {
                // Recorded now, against the rates in force today. The provider is
                // about to act on this number, and a report that recomputed it later
                // would restate settled history every time a rate changed.
                PlatformChargeKobo = charge.PlatformKeepsKobo,
            };

            // Persisted before the gateway is called. A webhook can arrive while the
            // initialise response is still in flight and needs a row to land on.
            if (!await _unitOfWork.PaymentCommands.InsertAsync(payment))
                return Fail<PaymentDto>(ResponseMessages.UnexpectedError);

            var initialisation = await _gateway.InitialiseAsync(new GatewayChargeRequest(
                Reference: payment.Reference,
                AmountKobo: payment.AmountKobo,
                CustomerEmail: tenant.Email,
                Currency: payment.Currency,
                CallbackUrl: _callbacks.Resolve(callbackUrl),
                Metadata: new Dictionary<string, string>
                {
                    ["purpose"] = PaymentPurpose.TenancyRent.ToString(),
                    ["tenancyId"] = tenancyId.ToString(),
                    ["customerId"] = tenancy.TenantCustomerId.ToString(),
                },
                SubaccountCode: payout.SubaccountCode,
                PlatformChargeKobo: charge.PlatformKeepsKobo));

            if (!initialisation.IsSuccessful || string.IsNullOrWhiteSpace(initialisation.AuthorisationUrl))
            {
                payment.TryFail(PaymentStatus.Failed, initialisation.Error);
                await _unitOfWork.PaymentCommands.UpdateAsync(payment);
                await _unitOfWork.SaveAsync();
                return Fail<PaymentDto>(initialisation.Error ?? ResponseMessages.PaymentCouldNotStart);
            }

            payment.RecordInitialisation(initialisation.AuthorisationUrl, initialisation.ProviderReference);
            await _unitOfWork.PaymentCommands.UpdateAsync(payment);

            // Moved on only once there is a charge to wait for. A tenancy sitting at
            // AwaitingPayment with nothing in flight reads as the tenant having
            // started something they never did.
            if (tenancy.TryBeginPayment())
                await _unitOfWork.TenancyCommands.UpdateAsync(tenancy);

            await _unitOfWork.SaveAsync();

            _logger.LogInformation(
                "Started a tenancy payment {Reference} for {TotalKobo} kobo, of which we keep {PlatformKobo}",
                payment.Reference, charge.TotalKobo, charge.PlatformKeepsKobo);

            return new BaseResponse<PaymentDto>(ToDto(payment), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting payment for tenancy {TenancyId}", tenancyId);
            return Fail<PaymentDto>(ResponseMessages.UnexpectedError);
        }
    }

    private async Task<Tenancy?> LoadForEitherPartyAsync(Guid tenancyId, Guid userId)
    {
        var tenancy = await _unitOfWork.TenancyQueries.GetByIdAsync(tenancyId);

        if (tenancy is null) return null;

        return tenancy.LandlordCustomerId == userId || tenancy.TenantCustomerId == userId ? tenancy : null;
    }

    private async Task<PayoutAccount?> FindPayoutAccountAsync(Guid ownerId)
    {
        var accounts = await _unitOfWork.PayoutAccountQueries.QueryByIndexAsync(CustomerIndex, ownerId);
        return accounts.OrderByDescending(a => a.DateCreated).FirstOrDefault();
    }

    private async Task<bool> HasSettledPaymentAsync(Guid tenancyId)
    {
        var payments = await _unitOfWork.PaymentQueries.GetAllAsync(
            p => p.SubjectId == tenancyId && p.Purpose == PaymentPurpose.TenancyRent);

        return payments.Any(p => p.Status == PaymentStatus.Successful);
    }

    /// <summary>
    /// An attempt already in flight for the same amount, rather than a second one.
    /// </summary>
    /// <remarks>
    /// A tenant who closes the gateway tab and comes back should land on the charge
    /// they started, not create another. Matched on the amount too, because a quote
    /// that has since changed is a different charge.
    /// </remarks>
    private async Task<Payment?> FindReusablePendingAttemptAsync(Guid tenancyId, long totalKobo)
    {
        var payments = await _unitOfWork.PaymentQueries.GetAllAsync(
            p => p.SubjectId == tenancyId && p.Purpose == PaymentPurpose.TenancyRent);

        return payments
            .Where(p => p.Status == PaymentStatus.Pending
                && p.AmountKobo == totalKobo
                && !string.IsNullOrWhiteSpace(p.AuthorisationUrl))
            .OrderByDescending(p => p.DateCreated)
            .FirstOrDefault();
    }

    /// <summary>Tells the owner they are the reason nobody can pay them.</summary>
    private async Task NotifyOwnerPayoutMissingAsync(Tenancy tenancy)
    {
        try
        {
            var property = await _unitOfWork.PropertyQueries.GetByIdAsync(tenancy.PropertyId);

            var notification = new Notification(
                tenancy.LandlordCustomerId,
                NotificationType.TenancyPayoutAccountRequired,
                "Add an account to be paid into",
                $"Your tenant is ready to pay for \"{property?.Title ?? "your property"}\" and we have nowhere to send the money. Add a payout account to let it go through.",
                tenancy.PropertyId);

            await _unitOfWork.NotificationCommands.InsertAsync(notification);
            await _unitOfWork.SaveAsync();
        }
        catch (Exception ex)
        {
            // Never costs the caller their error message. They are already being
            // told, correctly, that the owner cannot be paid yet.
            _logger.LogError(ex, "Could not tell owner {OwnerId} to add a payout account", tenancy.LandlordCustomerId);
        }
    }

    private static PaymentDto ToDto(Payment p) => new(
        p.Id, p.Reference, p.Purpose, p.SubjectId, p.AmountKobo, p.PurposeFeeKobo,
        p.IdentityFeeKobo, p.IncludesIdentityVerification, p.Currency, p.Status,
        p.AuthorisationUrl, p.PaidAt, p.DateCreated);

    private static BaseResponse<T> Fail<T>(string message) =>
        new(default, false, string.Empty, message);
}
