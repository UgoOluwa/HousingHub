using HousingHub.Model.Entities;
using HousingHub.Model.Enums;

namespace HousingHub.Test.Tenancies;

/// <summary>
/// Moving a tenancy from agreed to running.
/// </summary>
public class TenancyPaymentTests
{
    private static Tenancy Agreed(TenancyStatus status = TenancyStatus.DocumentsAccepted) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150_000_00, PropertyLeaseType.Rent)
        {
            Id = Guid.NewGuid(),
            Status = status,
        };

    [Fact]
    public void OnceEveryDocumentIsAccepted_PaymentCanStart()
    {
        var tenancy = Agreed();

        Assert.True(tenancy.TryBeginPayment());
        Assert.Equal(TenancyStatus.AwaitingPayment, tenancy.Status);
    }

    /// <summary>
    /// Charging before the paperwork is settled is charging for an agreement that
    /// does not exist yet — the documents are what say what was agreed.
    /// </summary>
    [Theory]
    [InlineData(TenancyStatus.CandidateSelected)]
    [InlineData(TenancyStatus.DocumentsRequested)]
    [InlineData(TenancyStatus.Withdrawn)]
    [InlineData(TenancyStatus.DeclinedByCandidate)]
    public void BeforeTheDocumentsAreAgreed_PaymentCannotStart(TenancyStatus status)
    {
        var tenancy = Agreed(status);

        Assert.False(tenancy.TryBeginPayment());
        Assert.Equal(status, tenancy.Status);
    }

    [Fact]
    public void WhenTheMoneyArrives_TheLetIsRunning()
    {
        var tenancy = Agreed(TenancyStatus.AwaitingPayment);

        Assert.True(tenancy.TryActivate());
        Assert.Equal(TenancyStatus.Active, tenancy.Status);
    }

    /// <summary>
    /// A provider's webhook can land before the browser comes back from the
    /// gateway, so the tenancy may never be observed awaiting payment. Refusing
    /// that would lose a paid tenancy to a race nobody can see.
    /// </summary>
    [Fact]
    public void AWebhookArrivingBeforeTheBrowser_StillActivates()
    {
        var tenancy = Agreed(TenancyStatus.DocumentsAccepted);

        Assert.True(tenancy.TryActivate());
        Assert.Equal(TenancyStatus.Active, tenancy.Status);
    }

    /// <summary>Providers retry. A replay must change nothing.</summary>
    [Fact]
    public void AReplayedWebhook_LeavesAnActiveTenancyAlone()
    {
        var tenancy = Agreed(TenancyStatus.AwaitingPayment);
        tenancy.TryActivate();

        Assert.False(tenancy.TryActivate());
        Assert.Equal(TenancyStatus.Active, tenancy.Status);
    }

    /// <summary>
    /// A tenancy somebody pulled out of must not come back to life because a
    /// payment landed late.
    /// </summary>
    [Theory]
    [InlineData(TenancyStatus.Withdrawn)]
    [InlineData(TenancyStatus.DeclinedByCandidate)]
    [InlineData(TenancyStatus.Ended)]
    public void AClosedTenancy_IsNotActivatedByALatePayment(TenancyStatus status)
    {
        var tenancy = Agreed(status);

        Assert.False(tenancy.TryActivate());
        Assert.Equal(status, tenancy.Status);
    }

    /// <summary>Neither party can walk away once the let is running and paid for.</summary>
    [Fact]
    public void ARunningLet_CannotBeWithdrawnOrDeclined()
    {
        var tenancy = Agreed(TenancyStatus.AwaitingPayment);
        tenancy.TryActivate();

        Assert.False(tenancy.TryWithdraw("changed my mind"));
        Assert.False(tenancy.TryDecline());
        Assert.Equal(TenancyStatus.Active, tenancy.Status);
    }
}
