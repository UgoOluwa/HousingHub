using System.Linq.Expressions;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.NotificationService.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PropertyEntity = HousingHub.Model.Entities.Property;
using TenancyEntity = HousingHub.Model.Entities.Tenancy;
using TenancyServiceImpl = HousingHub.Service.TenancyService.TenancyService;

namespace HousingHub.Test.Tenancies;

/// <summary>
/// Choosing a tenant: who may, from whom, and what it does to the listing.
/// </summary>
public class TenancyServiceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid CandidateId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid InspectionId = Guid.NewGuid();

    private readonly Mock<IUnitOfWOrk> _unitOfWork = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IRealtimeNotifier> _realtime = new();
    private readonly List<TenancyEntity> _inserted = [];

    private readonly TenancyServiceImpl _sut;

    public TenancyServiceTests()
    {
        _unitOfWork
            .Setup(u => u.TenancyCommands.InsertAsync(It.IsAny<TenancyEntity>()))
            .Callback<TenancyEntity>(_inserted.Add)
            .ReturnsAsync(true);
        _unitOfWork.Setup(u => u.TenancyCommands.UpdateAsync(It.IsAny<TenancyEntity>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<PropertyEntity>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>())).ReturnsAsync(true);
        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        // Nothing on any index unless a test says otherwise.
        _unitOfWork
            .Setup(u => u.TenancyQueries.QueryByIndexAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new List<TenancyEntity>());
        _unitOfWork
            .Setup(u => u.PropertyInspectionQueries.QueryByIndexAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new List<PropertyInspection>());

        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(
            new Customer("Ada", "Obi", "ada@test.com", "08000000000", CustomerType.Customer, "hash")
            {
                Id = CandidateId,
            });

        _sut = new TenancyServiceImpl(
            _unitOfWork.Object, _email.Object, _realtime.Object,
            NullLogger<TenancyServiceImpl>.Instance);
    }

    // ── fixtures ─────────────────────────────────────────────────

    private PropertyEntity GivenProperty(
        decimal price = 350_000m,
        PropertyAvailability availability = PropertyAvailability.Available,
        Guid? ownerId = null)
    {
        var property = new PropertyEntity(
            "Omolayo Estate", "A flat", PropertyType.Apartment, price,
            availability, PropertyLeaseType.Rent)
        {
            Id = PropertyId,
            OwnerId = ownerId ?? OwnerId,
        };

        _unitOfWork.Setup(u => u.PropertyQueries.GetByIdAsync(PropertyId)).ReturnsAsync(property);
        return property;
    }

    private void GivenInspection(InspectionStatus status = InspectionStatus.Completed, Guid? customerId = null)
    {
        var inspection = new PropertyInspection(
            customerId ?? CandidateId, PropertyId, DateTime.UtcNow.AddDays(-2), TimeSpan.FromHours(10), null)
        {
            Id = InspectionId,
            Status = status,
        };

        _unitOfWork
            .Setup(u => u.PropertyInspectionQueries.QueryByIndexAsync("PropertyId-index", It.IsAny<object>()))
            .ReturnsAsync(new List<PropertyInspection> { inspection });
    }

    private TenancyEntity GivenExistingTenancy(TenancyStatus status)
    {
        var tenancy = new TenancyEntity(
            PropertyId, OwnerId, CandidateId, InspectionId, 35_000_000, PropertyLeaseType.Rent)
        {
            Status = status,
        };

        _unitOfWork
            .Setup(u => u.TenancyQueries.QueryByIndexAsync("PropertyId-index", It.IsAny<object>()))
            .ReturnsAsync(new List<TenancyEntity> { tenancy });
        _unitOfWork.Setup(u => u.TenancyQueries.GetByIdAsync(tenancy.Id)).ReturnsAsync(tenancy);

        return tenancy;
    }

    // ── selecting ────────────────────────────────────────────────

    [Fact]
    public async Task Select_AChosenCandidate_CreatesTheTenancy()
    {
        GivenProperty();
        GivenInspection();

        var result = await _sut.SelectCandidateAsync(PropertyId, CandidateId, OwnerId);

        Assert.True(result.IsSuccessful);
        var tenancy = Assert.Single(_inserted);
        Assert.Equal(TenancyStatus.CandidateSelected, tenancy.Status);
        Assert.Equal(OwnerId, tenancy.LandlordCustomerId);
        Assert.Equal(CandidateId, tenancy.TenantCustomerId);
        Assert.Equal(InspectionId, tenancy.SelectedFromInspectionId);
    }

    /// <summary>
    /// The rent is captured at selection, not read back off the listing later. A
    /// listing can be edited and what was agreed must not move underneath either side.
    /// </summary>
    [Fact]
    public async Task Select_SnapshotsTheRentInKobo()
    {
        GivenProperty(price: 350_000m);
        GivenInspection();

        await _sut.SelectCandidateAsync(PropertyId, CandidateId, OwnerId);

        Assert.Equal(35_000_000, Assert.Single(_inserted).AgreedRentKobo);
    }

    [Fact]
    public async Task Select_PutsTheListingUnderOffer()
    {
        var property = GivenProperty();
        GivenInspection();

        await _sut.SelectCandidateAsync(PropertyId, CandidateId, OwnerId);

        Assert.Equal(PropertyAvailability.UnderOffer, property.Availability);
    }

    /// <summary>Email as well as in-app: this one changes where somebody lives.</summary>
    [Fact]
    public async Task Select_TellsTheCandidate_InAppAndByEmail()
    {
        GivenProperty();
        GivenInspection();

        await _sut.SelectCandidateAsync(PropertyId, CandidateId, OwnerId);

        _unitOfWork.Verify(u => u.NotificationCommands.InsertAsync(
            It.Is<Notification>(n => n.RecipientId == CandidateId)), Times.Once);
        _email.Verify(e => e.SendTenancyCandidateSelectedAsync(
            "ada@test.com", "Ada", "Omolayo Estate", 35_000_000), Times.Once);
    }

    /// <summary>
    /// You can only choose from people who actually viewed the property. Somebody who
    /// booked and never attended has not seen it.
    /// </summary>
    [Theory]
    [InlineData(InspectionStatus.Pending)]
    [InlineData(InspectionStatus.Confirmed)]
    [InlineData(InspectionStatus.Declined)]
    [InlineData(InspectionStatus.Cancelled)]
    public async Task Select_SomebodyWhoDidNotCompleteAnInspection_IsRefused(InspectionStatus status)
    {
        GivenProperty();
        GivenInspection(status);

        var result = await _sut.SelectCandidateAsync(PropertyId, CandidateId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.TenancyCandidateNotInspected, result.Message);
        Assert.Empty(_inserted);
    }

    [Fact]
    public async Task Select_SomebodyWhoNeverInspectedAtAll_IsRefused()
    {
        GivenProperty();
        GivenInspection(customerId: Guid.NewGuid());

        var result = await _sut.SelectCandidateAsync(PropertyId, CandidateId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Empty(_inserted);
    }

    /// <summary>Two people cannot both be taking the same flat.</summary>
    [Theory]
    [InlineData(TenancyStatus.CandidateSelected)]
    [InlineData(TenancyStatus.DocumentsRequested)]
    [InlineData(TenancyStatus.AwaitingPayment)]
    [InlineData(TenancyStatus.Active)]
    public async Task Select_WhenSomebodyIsAlreadyChosen_IsRefused(TenancyStatus existing)
    {
        GivenProperty();
        GivenInspection();
        GivenExistingTenancy(existing);

        var result = await _sut.SelectCandidateAsync(PropertyId, CandidateId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.TenancyAlreadyLive, result.Message);
        Assert.Empty(_inserted);
    }

    /// <summary>But a closed one releases the property, so a new choice is allowed.</summary>
    [Theory]
    [InlineData(TenancyStatus.Withdrawn)]
    [InlineData(TenancyStatus.DeclinedByCandidate)]
    [InlineData(TenancyStatus.Ended)]
    public async Task Select_AfterAPreviousOneClosed_IsAllowed(TenancyStatus closed)
    {
        GivenProperty();
        GivenInspection();
        GivenExistingTenancy(closed);

        var result = await _sut.SelectCandidateAsync(PropertyId, CandidateId, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Single(_inserted);
    }

    /// <summary>
    /// The agreement and the payment are both built on the rent. "Contact for price"
    /// is a listing state, not a tenancy state.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Select_OnAListingWithNoPrice_IsRefused(decimal price)
    {
        GivenProperty(price: price);
        GivenInspection();

        var result = await _sut.SelectCandidateAsync(PropertyId, CandidateId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.TenancyPriceNotSet, result.Message);
        Assert.Empty(_inserted);
    }

    /// <summary>Somebody else's property is indistinguishable from one that does not exist.</summary>
    [Fact]
    public async Task Select_OnSomebodyElsesProperty_ReportsNotFound()
    {
        GivenProperty(ownerId: Guid.NewGuid());
        GivenInspection();

        var result = await _sut.SelectCandidateAsync(PropertyId, CandidateId, Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.SetNotFoundMessage("property"), result.Message);
        Assert.Empty(_inserted);
    }

    // ── withdrawing and declining ────────────────────────────────

    [Fact]
    public async Task Withdraw_ReleasesTheListingAndTellsTheCandidate()
    {
        var property = GivenProperty(availability: PropertyAvailability.UnderOffer);
        var tenancy = GivenExistingTenancy(TenancyStatus.CandidateSelected);

        var result = await _sut.WithdrawAsync(tenancy.Id, "Taking it off the market", OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TenancyStatus.Withdrawn, tenancy.Status);
        Assert.Equal("Taking it off the market", tenancy.WithdrawnReason);
        Assert.Equal(PropertyAvailability.Available, property.Availability);
        _unitOfWork.Verify(u => u.NotificationCommands.InsertAsync(
            It.Is<Notification>(n => n.RecipientId == CandidateId)), Times.Once);
    }

    [Fact]
    public async Task Decline_ReleasesTheListingAndTellsTheOwner()
    {
        var property = GivenProperty(availability: PropertyAvailability.UnderOffer);
        var tenancy = GivenExistingTenancy(TenancyStatus.CandidateSelected);

        var result = await _sut.DeclineAsync(tenancy.Id, CandidateId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TenancyStatus.DeclinedByCandidate, tenancy.Status);
        Assert.Equal(PropertyAvailability.Available, property.Availability);
        _unitOfWork.Verify(u => u.NotificationCommands.InsertAsync(
            It.Is<Notification>(n => n.RecipientId == OwnerId)), Times.Once);
    }

    /// <summary>
    /// A listing the owner has since marked Rented or Sold must not be dragged back
    /// to Available by a stale tenancy closing.
    /// </summary>
    [Theory]
    [InlineData(PropertyAvailability.Rented)]
    [InlineData(PropertyAvailability.Sold)]
    public async Task Withdraw_DoesNotReopenAListingThatMovedOn(PropertyAvailability availability)
    {
        var property = GivenProperty(availability: availability);
        var tenancy = GivenExistingTenancy(TenancyStatus.CandidateSelected);

        await _sut.WithdrawAsync(tenancy.Id, null, OwnerId);

        Assert.Equal(availability, property.Availability);
    }

    [Fact]
    public async Task Withdraw_ByAnybodyButTheOwner_IsRefused()
    {
        GivenProperty(availability: PropertyAvailability.UnderOffer);
        var tenancy = GivenExistingTenancy(TenancyStatus.CandidateSelected);

        var result = await _sut.WithdrawAsync(tenancy.Id, null, CandidateId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TenancyStatus.CandidateSelected, tenancy.Status);
    }

    [Fact]
    public async Task Decline_ByAnybodyButTheChosenCandidate_IsRefused()
    {
        GivenProperty(availability: PropertyAvailability.UnderOffer);
        var tenancy = GivenExistingTenancy(TenancyStatus.CandidateSelected);

        var result = await _sut.DeclineAsync(tenancy.Id, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TenancyStatus.CandidateSelected, tenancy.Status);
    }

    /// <summary>
    /// Once the let is running, money has moved and somebody is living there. Ending
    /// it is a different act and belongs to the tenancy-management phase.
    /// </summary>
    [Fact]
    public async Task Withdraw_OnceTheLetIsRunning_IsRefused()
    {
        GivenProperty(availability: PropertyAvailability.Rented);
        var tenancy = GivenExistingTenancy(TenancyStatus.Active);

        var result = await _sut.WithdrawAsync(tenancy.Id, null, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.TenancyNotCancellable, result.Message);
        Assert.Equal(TenancyStatus.Active, tenancy.Status);
    }

    [Fact]
    public async Task Withdraw_Twice_OnlyClosesOnce()
    {
        GivenProperty(availability: PropertyAvailability.UnderOffer);
        var tenancy = GivenExistingTenancy(TenancyStatus.CandidateSelected);

        await _sut.WithdrawAsync(tenancy.Id, "First", OwnerId);
        var closedAt = tenancy.ClosedAt;
        var second = await _sut.WithdrawAsync(tenancy.Id, "Second", OwnerId);

        Assert.False(second.IsSuccessful);
        Assert.Equal("First", tenancy.WithdrawnReason);
        Assert.Equal(closedAt, tenancy.ClosedAt);
    }

    // ── reading ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ByAStranger_ReportsNotFound()
    {
        GivenProperty();
        var tenancy = GivenExistingTenancy(TenancyStatus.CandidateSelected);

        var result = await _sut.GetAsync(tenancy.Id, Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_ByEitherParty_Succeeds(bool asOwner)
    {
        GivenProperty();
        var tenancy = GivenExistingTenancy(TenancyStatus.CandidateSelected);

        var result = await _sut.GetAsync(tenancy.Id, asOwner ? OwnerId : CandidateId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
    }

    // ── candidates ───────────────────────────────────────────────

    [Fact]
    public async Task GetCandidates_ListsOnlyCompletedInspections()
    {
        GivenProperty();
        var other = Guid.NewGuid();
        _unitOfWork
            .Setup(u => u.PropertyInspectionQueries.QueryByIndexAsync("PropertyId-index", It.IsAny<object>()))
            .ReturnsAsync(new List<PropertyInspection>
            {
                new(CandidateId, PropertyId, DateTime.UtcNow.AddDays(-2), TimeSpan.FromHours(10), null)
                    { Id = InspectionId, Status = InspectionStatus.Completed },
                new(other, PropertyId, DateTime.UtcNow.AddDays(-1), TimeSpan.FromHours(11), null)
                    { Id = Guid.NewGuid(), Status = InspectionStatus.Pending },
            });
        _unitOfWork
            .Setup(u => u.CustomerQueries.GetManyByAsync(
                It.IsAny<Expression<Func<Customer, Guid>>>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new List<Customer>());

        var result = await _sut.GetCandidatesAsync(PropertyId, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(CandidateId, Assert.Single(result.Data!).CustomerId);
    }

    [Fact]
    public async Task GetCandidates_ForSomebodyElsesProperty_IsRefused()
    {
        GivenProperty(ownerId: Guid.NewGuid());

        var result = await _sut.GetCandidatesAsync(PropertyId, Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Empty(result.Data!);
    }
}
