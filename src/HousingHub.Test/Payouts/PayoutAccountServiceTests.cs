using System.Linq.Expressions;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Payments;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PayoutService;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HousingHub.Test.Payouts;

/// <summary>
/// Where an owner's rent is paid out to.
/// </summary>
/// <remarks>
/// Every test here is really about one question: can somebody point another
/// person's rent at an account they control. The name always comes from the bank
/// and is always checked against the identity an admin verified.
/// </remarks>
public class PayoutAccountServiceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private const string BankCode = "058";
    private const string AccountNumber = "0123456789";

    private readonly Mock<IUnitOfWOrk> _unitOfWork = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly PayoutAccountService _sut;

    public PayoutAccountServiceTests()
    {
        _sut = new PayoutAccountService(
            _unitOfWork.Object, _gateway.Object, NullLogger<PayoutAccountService>.Instance);

        _gateway.Setup(g => g.ListBanksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GatewayBank("Guaranty Trust Bank", BankCode)]);

        _unitOfWork.Setup(u => u.PayoutAccountQueries.QueryByIndexAsync(
                It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync([]);
        _unitOfWork.Setup(u => u.PayoutAccountCommands.InsertAsync(It.IsAny<PayoutAccount>()))
            .ReturnsAsync(true);
        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
    }

    private void SetupOwner(string first = "Bayo", string last = "Eze", bool verified = true)
    {
        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(OwnerId))
            .ReturnsAsync(new Customer(first, last, "b@e.com", "0800", CustomerType.HouseOwner, "hash")
            {
                Id = OwnerId,
                IsKycVerified = verified,
            });
    }

    private void SetupBankSays(string accountName) =>
        _gateway.Setup(g => g.ResolveAccountNameAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayAccountName(true, accountName, null));

    private void SetupSubaccount(string code = "ACCT_x1y2z3") =>
        _gateway.Setup(g => g.CreateSubaccountAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySubaccount(true, code, null));

    private static SavePayoutAccountDto Request() => new(BankCode, AccountNumber);

    // ── saving ───────────────────────────────────────────────────

    [Fact]
    public async Task AnAccountInTheOwnersName_IsAccepted()
    {
        SetupOwner();
        SetupBankSays("EZE BAYO CHUKWU");
        SetupSubaccount();

        var result = await _sut.SaveAsync(Request(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Guaranty Trust Bank", result.Data!.BankName);
        Assert.Equal("EZE BAYO CHUKWU", result.Data.ResolvedAccountName);
    }

    /// <summary>The whole point. Without this, rent goes wherever somebody types.</summary>
    [Fact]
    public async Task SomebodyElsesAccount_IsRefusedAndNothingIsCreated()
    {
        SetupOwner();
        SetupBankSays("ADA OBI");
        SetupSubaccount();

        var result = await _sut.SaveAsync(Request(), OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PayoutAccountNameMismatch, result.Message);

        _gateway.Verify(g => g.CreateSubaccountAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.PayoutAccountCommands.InsertAsync(It.IsAny<PayoutAccount>()), Times.Never);
    }

    /// <summary>
    /// A name the holder typed themselves proves nothing — both sides of the
    /// comparison would be their own words.
    /// </summary>
    [Fact]
    public async Task AnUnverifiedIdentity_CannotAddAPayoutAccountAtAll()
    {
        SetupOwner(verified: false);
        SetupBankSays("BAYO EZE");

        var result = await _sut.SaveAsync(Request(), OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PayoutAccountNeedsIdentity, result.Message);

        _gateway.Verify(g => g.ResolveAccountNameAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnAccountTheBankCannotFind_IsRefused()
    {
        SetupOwner();
        _gateway.Setup(g => g.ResolveAccountNameAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GatewayAccountName.Failed("We couldn't find that account."));

        var result = await _sut.SaveAsync(Request(), OwnerId);

        Assert.False(result.IsSuccessful);
        _unitOfWork.Verify(u => u.PayoutAccountCommands.InsertAsync(It.IsAny<PayoutAccount>()), Times.Never);
    }

    /// <summary>
    /// The number has no use once the destination exists — payouts are addressed by
    /// the subaccount — so keeping it would be holding a bank detail for nothing.
    /// </summary>
    [Fact]
    public async Task TheAccountNumberIsStoredMasked()
    {
        SetupOwner();
        SetupBankSays("BAYO EZE");
        SetupSubaccount();

        PayoutAccount? saved = null;
        _unitOfWork.Setup(u => u.PayoutAccountCommands.InsertAsync(It.IsAny<PayoutAccount>()))
            .Callback<PayoutAccount>(a => saved = a)
            .ReturnsAsync(true);

        await _sut.SaveAsync(Request(), OwnerId);

        Assert.NotNull(saved);
        Assert.Equal("******6789", saved!.AccountNumberMasked);
        Assert.DoesNotContain("012345", saved.AccountNumberMasked);
    }

    /// <summary>
    /// Two live payout accounts for one person is a question nobody should have to
    /// answer while money is moving.
    /// </summary>
    [Fact]
    public async Task ANewAccountReplacesTheOldOne()
    {
        SetupOwner();
        SetupBankSays("BAYO EZE");
        SetupSubaccount();

        var existing = new PayoutAccount(OwnerId, "011", "First Bank", "9999999999", "BAYO EZE", "ACCT_old");
        _unitOfWork.Setup(u => u.PayoutAccountQueries.QueryByIndexAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync([existing]);

        await _sut.SaveAsync(Request(), OwnerId);

        _unitOfWork.Verify(u => u.PayoutAccountCommands.DeleteAsync(existing), Times.Once);
        _unitOfWork.Verify(u => u.PayoutAccountCommands.InsertAsync(It.IsAny<PayoutAccount>()), Times.Once);
    }

    // ── resolving, before anything is created ────────────────────

    /// <summary>
    /// Shown, not judged — so a mistyped digit reads as the wrong name rather than
    /// as a refusal the owner has to guess at.
    /// </summary>
    [Fact]
    public async Task ResolvingSaysWhoseAccountItIsAndWhetherItMatches()
    {
        SetupOwner();
        SetupBankSays("ADA OBI");

        var result = await _sut.ResolveAsync(Request(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("ADA OBI", result.Data!.AccountName);
        Assert.False(result.Data.MatchesYourIdentity);
    }

    [Fact]
    public async Task ResolvingCreatesNothing()
    {
        SetupOwner();
        SetupBankSays("ADA OBI");

        await _sut.ResolveAsync(Request(), OwnerId);

        _gateway.Verify(g => g.CreateSubaccountAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── reading it back ──────────────────────────────────────────

    /// <summary>
    /// Not having one is the ordinary state of every owner who has never been paid,
    /// so it is a success with nothing in it rather than a failure.
    /// </summary>
    [Fact]
    public async Task AnOwnerWithNoPayoutAccount_GetsNullAndASuccess()
    {
        var result = await _sut.GetMineAsync(OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Data);
    }
}
