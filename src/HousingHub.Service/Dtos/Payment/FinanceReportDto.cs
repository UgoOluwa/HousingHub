using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Payment;

/// <summary>
/// What a single purpose brought in over the period.
/// </summary>
/// <remarks>
/// Split out because the two kinds of money behave differently: a verification fee
/// is ours entirely, while a rent payment is mostly somebody else's and passes
/// through us. A single revenue line covering both would be wrong by the size of
/// the rent, which on these amounts is nearly all of it.
/// </remarks>
public record FinancePurposeLineDto(
    PaymentPurpose Purpose,
    int Count,
    /// <summary>Everything charged to payers under this purpose.</summary>
    long GrossKobo,
    /// <summary>Paid straight through to owners. Never ours.</summary>
    long PaidToOwnersKobo,
    /// <summary>Our share before refunds.</summary>
    long EarnedKobo);

/// <summary>
/// The money that moved through Housing Hub over a period.
/// </summary>
/// <remarks>
/// <para>
/// Built from what each payment recorded at the time it was charged, never from
/// today's rates. Re-pricing settled history would make last month's report change
/// the next time a rate did, and the figures would stop matching the bank.
/// </para>
/// <para>
/// <b>Gross is not revenue.</b> Most of what passes through this system is rent on
/// its way to an owner. <see cref="NetEarnedKobo"/> is the only figure that is ours
/// to keep, and it is what a profit-and-loss line should read.
/// </para>
/// </remarks>
public record FinanceReportDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string Currency,

    /// <summary>Settled payments in the period.</summary>
    int SettledCount,

    /// <summary>Everything payers were charged. Includes money that was never ours.</summary>
    long GrossCollectedKobo,

    /// <summary>Rent and owners' fees, split straight to their accounts.</summary>
    long PaidToOwnersKobo,

    /// <summary>Our commission and the processing fee, before refunds.</summary>
    long EarnedKobo,

    /// <summary>Sent back to payers. Counted at what the provider actually returned.</summary>
    long RefundedKobo,

    int RefundedCount,

    /// <summary>What we kept. <see cref="EarnedKobo"/> less <see cref="RefundedKobo"/>.</summary>
    long NetEarnedKobo,

    /// <summary>
    /// Payments confirmed for an amount that did not match what was asked for.
    /// </summary>
    /// <remarks>
    /// Carried on the report because they are money received against nothing handed
    /// over — a figure that belongs in the accounts and in somebody's queue at the
    /// same time.
    /// </remarks>
    int FlaggedCount,
    long FlaggedKobo,

    /// <summary>Started and never completed. Not money, but it is what a failing checkout looks like.</summary>
    int PendingCount,
    int FailedCount,

    IReadOnlyList<FinancePurposeLineDto> ByPurpose);
