namespace HousingHub.Service.Dtos.Payment;

/// <summary>
/// An owner's payout account, as they see it back.
/// </summary>
/// <remarks>
/// Carries the masked number and the provider's handle is withheld entirely: the
/// subaccount code addresses money and has no use in a browser.
/// </remarks>
public record PayoutAccountDto(
    Guid Id,
    string BankName,
    string BankCode,
    string AccountNumberMasked,
    /// <summary>The name the bank returned, which is what was checked.</summary>
    string ResolvedAccountName,
    DateTime DateCreated);

public record BankDto(string Name, string Code);

/// <summary>What the owner submits. Nothing here is trusted beyond being well-formed.</summary>
public record SavePayoutAccountDto(string BankCode, string AccountNumber);

/// <summary>
/// The name a bank gives for an account, before anything is created.
/// </summary>
/// <remarks>
/// Exists so the owner can see whose account they have typed before committing to
/// it — the commonest cause of a failed match is a mistyped digit, and finding
/// that out as a refusal is worse than seeing the wrong name.
/// </remarks>
public record ResolvedAccountDto(string AccountName, bool MatchesYourIdentity);
