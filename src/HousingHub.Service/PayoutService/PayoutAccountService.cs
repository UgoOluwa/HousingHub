using HousingHub.Core.CustomResponses;
using HousingHub.Core.Security;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Commons.Payments;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PayoutService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PayoutService;

/// <inheritdoc cref="IPayoutAccountService"/>
public class PayoutAccountService : IPayoutAccountService
{
    private const string CustomerIndex = "CustomerId-index";

    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<PayoutAccountService> _logger;

    public PayoutAccountService(
        IUnitOfWOrk unitOfWork,
        IPaymentGateway gateway,
        ILogger<PayoutAccountService> logger)
    {
        _unitOfWork = unitOfWork;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<BaseResponse<List<BankDto>>> GetBanksAsync()
    {
        try
        {
            var banks = await _gateway.ListBanksAsync();

            return new BaseResponse<List<BankDto>>(
                banks.Select(b => new BankDto(b.Name, b.Code)).ToList(),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing banks");
            return new BaseResponse<List<BankDto>>([], false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<ResolvedAccountDto>> ResolveAsync(
        SavePayoutAccountDto request, Guid authenticatedUserId)
    {
        try
        {
            var (customer, failure) = await LoadVerifiedOwnerAsync(authenticatedUserId);
            if (failure is not null) return Fail<ResolvedAccountDto>(failure);

            var resolved = await _gateway.ResolveAccountNameAsync(request.AccountNumber, request.BankCode);
            if (!resolved.IsSuccessful || string.IsNullOrWhiteSpace(resolved.AccountName))
                return Fail<ResolvedAccountDto>(resolved.Error ?? ResponseMessages.PayoutAccountNameUnavailable);

            // Shown rather than judged. The owner sees whose account they have typed
            // and can correct a digit; the decision is still made at save, against
            // the bank's answer rather than this one.
            return new BaseResponse<ResolvedAccountDto>(
                new ResolvedAccountDto(
                    resolved.AccountName,
                    BankAccountNameMatcher.Matches(VerifiedName(customer!), resolved.AccountName)),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving a payout account for {CustomerId}", authenticatedUserId);
            return Fail<ResolvedAccountDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<PayoutAccountDto>> SaveAsync(
        SavePayoutAccountDto request, Guid authenticatedUserId)
    {
        try
        {
            var (customer, failure) = await LoadVerifiedOwnerAsync(authenticatedUserId);
            if (failure is not null) return Fail<PayoutAccountDto>(failure);

            var resolved = await _gateway.ResolveAccountNameAsync(request.AccountNumber, request.BankCode);
            if (!resolved.IsSuccessful || string.IsNullOrWhiteSpace(resolved.AccountName))
                return Fail<PayoutAccountDto>(resolved.Error ?? ResponseMessages.PayoutAccountNameUnavailable);

            // The check the whole feature exists for. Re-run here rather than trusted
            // from the resolve call, because the two are separate requests and only
            // this one creates a destination money can reach.
            if (!BankAccountNameMatcher.Matches(VerifiedName(customer!), resolved.AccountName))
            {
                _logger.LogWarning(
                    "Refused a payout account for {CustomerId}: the bank's name does not match their verified identity",
                    authenticatedUserId);

                return Fail<PayoutAccountDto>(ResponseMessages.PayoutAccountNameMismatch);
            }

            var subaccount = await _gateway.CreateSubaccountAsync(
                resolved.AccountName!, request.BankCode, request.AccountNumber);

            if (!subaccount.IsSuccessful || string.IsNullOrWhiteSpace(subaccount.SubaccountCode))
                return Fail<PayoutAccountDto>(subaccount.Error ?? ResponseMessages.UnexpectedError);

            var banks = await _gateway.ListBanksAsync();
            var bankName = banks.FirstOrDefault(b => b.Code == request.BankCode)?.Name ?? request.BankCode;

            var account = new PayoutAccount(
                authenticatedUserId, request.BankCode, bankName,
                request.AccountNumber, resolved.AccountName!, subaccount.SubaccountCode!);

            // Replaced, not added. Two live payout accounts for one person is a
            // question nobody should have to answer while money is moving.
            foreach (var existing in await _unitOfWork.PayoutAccountQueries.QueryByIndexAsync(
                CustomerIndex, authenticatedUserId))
            {
                await _unitOfWork.PayoutAccountCommands.DeleteAsync(existing);
            }

            await _unitOfWork.PayoutAccountCommands.InsertAsync(account);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("Payout account set for {CustomerId}", authenticatedUserId);

            return new BaseResponse<PayoutAccountDto>(
                ToDto(account), true, string.Empty, ResponseMessages.PayoutAccountSaved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving a payout account for {CustomerId}", authenticatedUserId);
            return Fail<PayoutAccountDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<PayoutAccountDto?>> GetMineAsync(Guid authenticatedUserId)
    {
        try
        {
            var existing = await _unitOfWork.PayoutAccountQueries.QueryByIndexAsync(
                CustomerIndex, authenticatedUserId);

            var account = existing.OrderByDescending(a => a.DateCreated).FirstOrDefault();

            // Null with a success, not a failure. Not having set one up yet is the
            // ordinary state of every owner who has never been paid.
            return new BaseResponse<PayoutAccountDto?>(
                account is null ? null : ToDto(account), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading the payout account for {CustomerId}", authenticatedUserId);
            return new BaseResponse<PayoutAccountDto?>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// The customer, if we know who they are well enough to check an account against.
    /// </summary>
    /// <remarks>
    /// Identity verification is the premise of the whole check: matching a bank's
    /// name against a self-declared one proves nothing, because both were typed by
    /// the same person.
    /// </remarks>
    private async Task<(Customer? Customer, string? Failure)> LoadVerifiedOwnerAsync(Guid customerId)
    {
        var customer = await _unitOfWork.CustomerQueries.GetByIdAsync(customerId);

        if (customer is null)
            return (null, ResponseMessages.SetNotFoundMessage("customer"));

        if (!customer.IsKycVerified)
            return (null, ResponseMessages.PayoutAccountNeedsIdentity);

        return (customer, null);
    }

    private static string VerifiedName(Customer customer) =>
        $"{customer.FirstName} {customer.LastName}".Trim();

    private static PayoutAccountDto ToDto(PayoutAccount a) =>
        new(a.Id, a.BankName, a.BankCode, a.AccountNumberMasked, a.ResolvedAccountName, a.DateCreated);

    private static BaseResponse<T> Fail<T>(string message) =>
        new(default, false, string.Empty, message);
}
