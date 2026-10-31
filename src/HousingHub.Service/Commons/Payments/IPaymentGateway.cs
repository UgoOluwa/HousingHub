namespace HousingHub.Service.Commons.Payments;

/// <summary>Where a gateway says a transaction got to.</summary>
public enum GatewayTransactionStatus
{
    Pending = 1,
    Successful = 2,
    Failed = 3,
    Abandoned = 4,
}

/// <summary>What we ask a gateway to collect. The amount is never client-supplied.</summary>
public sealed record GatewayChargeRequest(
    string Reference,
    long AmountKobo,
    string CustomerEmail,
    string Currency,
    string? CallbackUrl,
    IReadOnlyDictionary<string, string>? Metadata = null,

    /// <summary>
    /// The payout destination the balance of this charge belongs to.
    /// </summary>
    /// <remarks>
    /// Set only on a split. The provider pays this account everything except
    /// <see cref="PlatformChargeKobo"/>, so the rent reaches the owner without
    /// passing through us.
    /// </remarks>
    string? SubaccountCode = null,

    /// <summary>
    /// What we keep out of the charge, in kobo. The remainder goes to the subaccount.
    /// </summary>
    /// <remarks>
    /// A flat amount rather than a percentage, because the commission is a share of
    /// the rent and the fees differ on every let — the figure is only knowable here,
    /// at the point of charging.
    /// </remarks>
    long? PlatformChargeKobo = null);

/// <summary>The gateway's answer to "where should the payer go to pay this".</summary>
public sealed record GatewayInitialisation(
    bool IsSuccessful,
    string? AuthorisationUrl,
    string? ProviderReference,
    string? Error)
{
    public static GatewayInitialisation Failed(string error) => new(false, null, null, error);
    public static GatewayInitialisation Succeeded(string url, string? providerReference) =>
        new(true, url, providerReference, null);
}

/// <summary>The gateway's answer to a refund request.</summary>
public sealed record GatewayRefund(
    bool IsSuccessful,
    /// <summary>True when the provider says the money is already back.</summary>
    bool IsComplete,
    long? AmountKobo,
    string? RefundReference,
    string? Error)
{
    public static GatewayRefund Failed(string error) => new(false, false, null, null, error);
}

/// <summary>A transaction as the gateway reports it.</summary>
public sealed record GatewayTransaction(
    string Reference,
    GatewayTransactionStatus Status,
    long AmountKobo,
    string? ProviderReference,
    string? Channel,
    string? FailureReason);

/// <summary>One of the banks a payout account can be held with.</summary>
public sealed record GatewayBank(string Name, string Code);

/// <summary>
/// Whose name the bank says an account is in.
/// </summary>
/// <remarks>
/// The name comes from the bank, never from the person claiming the account. That
/// is the entire value of the call — anyone can type a name.
/// </remarks>
public sealed record GatewayAccountName(bool IsSuccessful, string? AccountName, string? Error)
{
    public static GatewayAccountName Failed(string error) => new(false, null, error);
}

/// <summary>A payout destination the provider will split money into.</summary>
public sealed record GatewaySubaccount(bool IsSuccessful, string? SubaccountCode, string? Error)
{
    public static GatewaySubaccount Failed(string error) => new(false, null, error);
}

/// <summary>
/// A payment provider.
/// </summary>
/// <remarks>
/// <para>
/// An interface with one implementation today, for the same reason
/// <c>IGeocodingService</c> and <c>ICacLookupService</c> are: the provider is a
/// commercial decision that will be revisited, and the alternative is a service
/// layer that knows Paystack's JSON shape.
/// </para>
/// <para>
/// It also makes the security-critical paths testable without a network. A fake can
/// return a forged signature, a replayed body, or a confirmed payment for the wrong
/// amount — the three cases that matter and the three you cannot arrange against a
/// real sandbox.
/// </para>
/// </remarks>
public interface IPaymentGateway
{
    /// <summary>Provider name, recorded on the payment so a later migration can tell attempts apart.</summary>
    string Name { get; }

    /// <summary>
    /// Whether the configured credential is a test one.
    /// </summary>
    /// <remarks>
    /// Read from the key itself rather than from a setting. Anything that relaxes a
    /// check for testing has to be switched on by the same fact that makes it safe —
    /// a separate flag is one somebody can set in production.
    /// </remarks>
    bool IsTestMode { get; }

    /// <summary>Registers the charge and returns where to send the payer.</summary>
    Task<GatewayInitialisation> InitialiseAsync(GatewayChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the gateway what actually happened to a transaction.
    /// </summary>
    /// <remarks>
    /// The authority on whether money moved. A browser returning to a callback URL
    /// proves nothing at all — the payer controls that redirect — and even a
    /// signature-verified webhook body is worth re-reading from source before
    /// anything is handed over. Returns null when the gateway has never heard of
    /// the reference.
    /// </remarks>
    Task<GatewayTransaction?> GetTransactionAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the provider to send money back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The amount is passed explicitly rather than inferred, because the figure that
    /// should go back is what actually arrived — which for a flagged payment is not
    /// what was asked for.
    /// </para>
    /// <para>
    /// Most providers answer "pending" and confirm by webhook, so
    /// <c>IsSuccessful</c> means the request was accepted, not that the money has
    /// moved. <c>IsComplete</c> is the one that means moved.
    /// </para>
    /// </remarks>
    /// <summary>Banks a payout account can be held with, for the owner to pick from.</summary>
    Task<IReadOnlyList<GatewayBank>> ListBanksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the bank whose name an account is in.
    /// </summary>
    /// <remarks>
    /// Checked against the holder's verified identity before any payout destination
    /// is created. Without it, anyone who can list a property can point rent at any
    /// account number they can type.
    /// </remarks>
    Task<GatewayAccountName> ResolveAccountNameAsync(
        string accountNumber, string bankCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the payout destination a split will pay into.
    /// </summary>
    /// <remarks>
    /// Created taking none of the transaction by default. What we keep is decided
    /// per transaction instead, because the commission is a share of the rent and
    /// the fees differ on every let — baking a percentage in here would fix at
    /// onboarding a number that is only knowable at payment.
    /// </remarks>
    Task<GatewaySubaccount> CreateSubaccountAsync(
        string businessName,
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default);

    Task<GatewayRefund> RefundAsync(
        string transactionReference,
        long amountKobo,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a webhook body genuinely came from the provider.
    /// </summary>
    /// <remarks>
    /// Takes the <b>raw body</b>, not a parsed model. A signature is computed over
    /// exact bytes, so deserialising and re-serialising before checking it — the
    /// obvious thing to do with an ASP.NET action — changes whitespace and key order
    /// and makes every legitimate signature fail.
    /// </remarks>
    bool IsWebhookAuthentic(string rawBody, string? signatureHeader);
}
