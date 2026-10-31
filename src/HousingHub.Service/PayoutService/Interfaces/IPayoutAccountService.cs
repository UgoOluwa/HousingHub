using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Payment;

namespace HousingHub.Service.PayoutService.Interfaces;

/// <summary>
/// Where an owner's rent is paid out to.
/// </summary>
/// <remarks>
/// Every method here is about one question: does this bank account belong to the
/// person claiming it. Get that wrong and somebody's rent goes to a stranger, so
/// the name always comes from the bank and is always checked against the identity
/// an admin verified — never against what was typed.
/// </remarks>
public interface IPayoutAccountService
{
    /// <summary>Banks to choose from.</summary>
    Task<BaseResponse<List<BankDto>>> GetBanksAsync();

    /// <summary>Asks the bank whose account this is, without creating anything.</summary>
    Task<BaseResponse<ResolvedAccountDto>> ResolveAsync(SavePayoutAccountDto request, Guid authenticatedUserId);

    /// <summary>Checks the account and, if it is theirs, makes it their payout destination.</summary>
    Task<BaseResponse<PayoutAccountDto>> SaveAsync(SavePayoutAccountDto request, Guid authenticatedUserId);

    /// <summary>The caller's own payout account, or null when they have not set one.</summary>
    Task<BaseResponse<PayoutAccountDto?>> GetMineAsync(Guid authenticatedUserId);
}
