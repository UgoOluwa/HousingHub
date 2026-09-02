using HousingHub.Model.Enums;
using Microsoft.Extensions.Configuration;

namespace HousingHub.Service.Commons.Payments;

/// <summary>
/// What each thing costs, and whether charging is switched on at all.
/// </summary>
/// <remarks>
/// <para>
/// Prices are configuration, never code. They are a commercial decision that will
/// change without a deploy, they differ between dev and production, and nobody
/// should have to read C# to find out what a customer is charged.
/// </para>
/// <para>
/// <b>Kobo, as whole numbers, in configuration too.</b> A price written as naira
/// with a decimal point invites a parse that silently loses the kobo, and a fee of
/// "5000" that was meant to be ₦5,000 but is read as ₦50 is a bug nobody notices
/// until the month-end reconciliation.
/// </para>
/// </remarks>
public class PaymentFeeCatalogue
{
    public const string EnabledKey = "Payments:Enabled";
    public const string CurrencyKey = "Payments:Currency";
    private const string FeeKeyPrefix = "Payments:Fees:";

    private readonly IConfiguration _configuration;

    public PaymentFeeCatalogue(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Whether anything is charged for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off by default, and the reason is deployment order. The whole payment path can
    /// ship, be deployed and be exercised against a sandbox while verification stays
    /// free — because until this is true, <c>SubmitCaseAsync</c> does not ask for a
    /// payment and every existing flow behaves exactly as it did.
    /// </para>
    /// <para>
    /// Turning it on is a commercial decision, not a deployment one — the same
    /// reasoning as <c>Verification:ShowTitleBadge</c>.
    /// </para>
    /// </remarks>
    public bool IsEnabled => _configuration.GetValue(EnabledKey, false);

    public string Currency => _configuration[CurrencyKey]?.Trim() is { Length: > 0 } c ? c : "NGN";

    /// <summary>
    /// The price of one item, in kobo.
    /// </summary>
    /// <remarks>
    /// <b>An unset or non-positive price is an error, never a free item.</b> Falling
    /// back to zero would mean a missing configuration entry silently gives away the
    /// thing it was supposed to price — and it would do it while every health check
    /// stayed green. Failing here surfaces as "payments are not configured" at the
    /// moment someone tries to pay, which is loud and fixable.
    /// </remarks>
    public bool TryGetFeeKobo(PaymentPurpose purpose, out long kobo, out string? error)
    {
        var key = FeeKeyPrefix + purpose;
        var raw = _configuration[key];

        if (string.IsNullOrWhiteSpace(raw))
        {
            kobo = 0;
            error = $"No price is configured for {purpose} ({key}).";
            return false;
        }

        if (!long.TryParse(raw.Trim(), out kobo) || kobo <= 0)
        {
            kobo = 0;
            error = $"The price configured for {purpose} ({key}) is not a positive whole number of kobo.";
            return false;
        }

        error = null;
        return true;
    }
}
