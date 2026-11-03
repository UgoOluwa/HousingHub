using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Payment;

namespace HousingHub.Test.Payments;

/// <summary>
/// Turning a payment into the shape a client reads.
/// </summary>
/// <remarks>
/// A positional record is mapped by argument order, and a second hand-written copy
/// of this call shifted the gateway link into Channel — so a payment registered
/// perfectly well came back with nowhere to send the payer, and the client showed
/// the success message as an error. These assertions are about position, which is
/// the thing the compiler cannot check.
/// </remarks>
public class PaymentDtoTests
{
    private const string Checkout = "https://checkout.paystack.com/xp3qk8z3aemcsmw";

    private static Payment Pending() =>
        new("HH-abc", Guid.NewGuid(), PaymentPurpose.TenancyRent, Guid.NewGuid(), 56_950_00, 0, "NGN", "paystack");

    /// <summary>
    /// The field the client redirects on. In the wrong slot there is no redirect,
    /// and nothing on screen says why.
    /// </summary>
    [Fact]
    public void AGatewayLinkOnAPendingPayment_ComesBackAsTheAuthorisationUrl()
    {
        var payment = Pending();
        payment.RecordInitialisation(Checkout, "ps_ref");

        var dto = PaymentDto.From(payment);

        Assert.Equal(Checkout, dto.AuthorisationUrl);
        Assert.Null(dto.Channel);
    }

    /// <summary>
    /// Channel is how the payer paid — card, transfer — not where to send them.
    /// The two sat next to each other and were swapped.
    /// </summary>
    [Fact]
    public void ChannelIsHowTheyPaid_NotWhereToSendThem()
    {
        var payment = Pending();
        payment.TrySettle(56_950_00, "ps_ref", "card");

        var dto = PaymentDto.From(payment);

        Assert.Equal("card", dto.Channel);
    }

    /// <summary>
    /// A stale gateway link on a settled payment is an invitation to pay twice.
    /// </summary>
    [Fact]
    public void ASettledPayment_IsNotHandedTheGatewayLinkAgain()
    {
        var payment = Pending();
        payment.RecordInitialisation(Checkout, "ps_ref");
        payment.TrySettle(56_950_00, "ps_ref", "card");

        Assert.Null(PaymentDto.From(payment).AuthorisationUrl);
    }

    [Fact]
    public void TheMoneyAndTheSubjectSurviveTheMapping()
    {
        var payment = Pending();

        var dto = PaymentDto.From(payment);

        Assert.Equal(payment.Reference, dto.Reference);
        Assert.Equal(payment.SubjectId, dto.SubjectId);
        Assert.Equal(56_950_00, dto.AmountKobo);
        Assert.Equal(PaymentPurpose.TenancyRent, dto.Purpose);
        Assert.Equal(PaymentStatus.Pending, dto.Status);
    }
}
