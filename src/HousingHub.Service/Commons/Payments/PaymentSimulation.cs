namespace HousingHub.Service.Commons.Payments;

/// <summary>
/// The simulated bank, for walking the payout flow without a real account.
/// </summary>
/// <remarks>
/// <para>
/// Resolving a bank account is metered — three live lookups a day on a test key —
/// and the name a real lookup returns has to match the owner's verified identity,
/// which makes testing the flow cost one of a developer's own bank accounts and a
/// third of the day's allowance per attempt.
/// </para>
/// <para>
/// <b>This bypasses the name check, which is the only thing standing between an
/// owner and somebody else's rent.</b> It is therefore gated on the provider's
/// credential being a test key, not on a setting — the bypass is switched on by the
/// same fact that makes it harmless. A live key cannot have it, because a live key
/// is not a test key; there is no combination of configuration that produces a
/// production environment with this active, and no flag anybody can set wrongly.
/// </para>
/// </remarks>
public static class PaymentSimulation
{
    /// <summary>
    /// Paystack's own test bank code. Resolves without counting against the daily
    /// limit on live lookups.
    /// </summary>
    public const string TestBankCode = "001";

    /// <summary>
    /// Says what it is in the dropdown, because somebody will see it on a shared
    /// dev environment and wonder.
    /// </summary>
    public const string TestBankName = "Test Bank — simulated, dev only";

    /// <summary>
    /// Whether this account should skip the identity check.
    /// </summary>
    /// <remarks>
    /// Both conditions, always. The bank code alone is not enough: "001" is a real
    /// string a live caller can send, and without the test-key check it would be a
    /// password for routing rent anywhere.
    /// </remarks>
    public static bool IsSimulated(string? bankCode, bool isTestMode) =>
        isTestMode && string.Equals(bankCode, TestBankCode, StringComparison.Ordinal);
}
