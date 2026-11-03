using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

/// <summary>
/// Where an owner's rent is paid out to.
/// </summary>
/// <remarks>
/// <para>
/// One per customer. Changing bank replaces the row rather than adding one — two
/// live payout accounts for the same person is a question nobody should have to
/// answer at the moment money is moving.
/// </para>
/// <para>
/// The account number is stored masked. Once the subaccount exists, the number has
/// no further use to us — payouts are addressed by
/// <see cref="SubaccountCode"/> — and keeping the full number would be holding a
/// bank detail we never need again.
/// </para>
/// </remarks>
[DynamoDBTable("PayoutAccounts")]
public class PayoutAccount : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("CustomerId-index")]
    public Guid CustomerId { get; set; }

    public string BankCode { get; set; } = null!;
    public string BankName { get; set; } = null!;

    /// <summary>Last four digits only, for the owner to recognise which account this is.</summary>
    public string AccountNumberMasked { get; set; } = null!;

    /// <summary>
    /// The name the bank returned for the account, not the name the owner typed.
    /// </summary>
    /// <remarks>
    /// This is the thing that was checked against their verified identity, so it is
    /// the thing worth keeping — what they typed proves nothing.
    /// </remarks>
    public string ResolvedAccountName { get; set; } = null!;

    /// <summary>The provider's handle for this payout destination. What a split actually addresses.</summary>
    public string SubaccountCode { get; set; } = null!;

    public PayoutAccount() { }

    public PayoutAccount(
        Guid customerId,
        string bankCode,
        string bankName,
        string accountNumber,
        string resolvedAccountName,
        string subaccountCode)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        BankCode = bankCode;
        BankName = bankName;
        AccountNumberMasked = Mask(accountNumber);
        ResolvedAccountName = resolvedAccountName;
        SubaccountCode = subaccountCode;
    }

    /// <summary>Shows the last four digits, so an owner can tell two of their accounts apart.</summary>
    public static string Mask(string accountNumber)
    {
        var digits = new string(accountNumber.Where(char.IsDigit).ToArray());

        return digits.Length <= 4 ? digits : $"******{digits[^4..]}";
    }
}
