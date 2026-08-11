using System.Linq.Expressions;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Commons.Geocoding;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.PropertyAlertService.Interfaces;
using HousingHub.Service.PropertyService;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
// Sibling namespaces under HousingHub.Test share simple names with entity classes
// (HousingHub.Test.PropertyAddress, .PropertyFile, .Admin, .CustomerAddress), and
// C#'s sibling-namespace lookup wins over the imported type — so these must be
// aliased or the bare name resolves to a namespace.
using PropertyEntity = HousingHub.Model.Entities.Property;
using PropertyAddressEntity = HousingHub.Model.Entities.PropertyAddress;

namespace HousingHub.Test.Authorization;

/// <summary>
/// Who is allowed to put a listing in front of the public.
/// </summary>
/// <remarks>
/// <para>
/// Every blocker found in the original security audit was an authorization bug, and
/// this layer had no coverage at all — which is precisely why they survived to be
/// found by audit rather than by CI.
/// </para>
/// <para>
/// The identity check in particular existed only as a redirect in
/// <c>AddPropertyForm.tsx</c>. A redirect is a convenience, not a control: POSTing
/// directly to the API put a listing live with no identity verification whatsoever.
/// These tests exist so that if the server-side gate is ever removed or refactored
/// away, the build fails instead of the product quietly reverting to trusting the
/// browser.
/// </para>
/// </remarks>
public class PropertyPublishAuthorizationTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWork;
    private readonly PropertyCommandService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();

    public PropertyPublishAuthorizationTests()
    {
        _unitOfWork = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };

        var config = new TypeAdapterConfig();
        new PropertyMapper().Register(config);

        _unitOfWork.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<PropertyEntity>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.PropertyAddressQueries.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((PropertyAddressEntity?)null);

        var alertPreferences = new Mock<IPropertyAlertPreferenceQueryService>();
        alertPreferences.Setup(p => p.GetAllActiveAsync()).ReturnsAsync(new List<PropertyAlertPreference>());

        _sut = new PropertyCommandService(
            NullLogger<PropertyCommandService>.Instance,
            _unitOfWork.Object,
            new ObjectMapper(config),
            new Mock<IFileStorageService>().Object,
            new Mock<IGeocodingService>().Object,
            new Mock<IEmailService>().Object,
            alertPreferences.Object,
            new Mock<IRealtimeNotifier>().Object);
    }

    /// <param name="kycVerified">Whether an admin has approved this person's ID.</param>
    /// <param name="kycSubmitted">Whether they have uploaded documents at all.</param>
    private Customer GivenCaller(
        Guid id,
        CustomerType type = CustomerType.HouseOwner,
        bool kycVerified = true,
        bool kycSubmitted = true)
    {
        var customer = new Customer("Ada", "Obi", "ada@test.com", "08012345678", type, "hash")
        {
            Id = id,
            IsKycVerified = kycVerified,
            KycSubmittedAt = kycSubmitted ? DateTime.UtcNow.AddDays(-1) : null,
        };

        _unitOfWork
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customer);

        return customer;
    }

    private PropertyEntity GivenPropertyOwnedBy(Guid ownerId, bool published = false)
    {
        var property = new PropertyEntity("Flat", "Desc", PropertyType.Apartment, 250000m,
            PropertyAvailability.Available, PropertyLeaseType.Rent)
        {
            Id = PropertyId,
            OwnerId = ownerId,
            IsPublished = published,
            AddressId = Guid.NewGuid(),
        };

        _unitOfWork
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<PropertyEntity, bool>>>()))
            .ReturnsAsync(property);

        return property;
    }

    private void AssertNothingWasPersisted() =>
        _unitOfWork.Verify(u => u.PropertyCommands.UpdateAsync(It.IsAny<PropertyEntity>()), Times.Never);

    // ── Identity verification gates publishing ───────────────────

    [Fact]
    public async Task Publish_ByVerifiedOwner_Succeeds()
    {
        GivenCaller(OwnerId);
        var property = GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.SetPropertyPublishedAsync(PropertyId, isPublished: true, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.True(property.IsPublished);
    }

    [Fact]
    public async Task Publish_ByOwnerWhoseIdIsStillInReview_IsRefusedAndChangesNothing()
    {
        // The exact bypass this gate closes: the frontend would have redirected this
        // person away, so the only way to reach here is by calling the API directly.
        GivenCaller(OwnerId, kycVerified: false, kycSubmitted: true);
        var property = GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.SetPropertyPublishedAsync(PropertyId, isPublished: true, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.False(property.IsPublished);
        AssertNothingWasPersisted();
    }

    [Fact]
    public async Task Publish_ByOwnerWhoNeverSubmittedId_IsRefused()
    {
        GivenCaller(OwnerId, kycVerified: false, kycSubmitted: false);
        GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.SetPropertyPublishedAsync(PropertyId, isPublished: true, OwnerId);

        Assert.False(result.IsSuccessful);
        AssertNothingWasPersisted();
    }

    [Fact]
    public async Task Publish_RefusalTellsTheOwnerWhichSituationTheyAreIn()
    {
        // "We're still checking" and "you haven't started" need different actions from
        // the user, so they must not collapse into one message. This is a legitimate
        // owner doing the right thing slightly early, not an attacker — there is no
        // enumeration concern here to justify a vague response.
        GivenCaller(OwnerId, kycVerified: false, kycSubmitted: true);
        GivenPropertyOwnedBy(OwnerId);
        var inReview = await _sut.SetPropertyPublishedAsync(PropertyId, true, OwnerId);

        GivenCaller(OwnerId, kycVerified: false, kycSubmitted: false);
        GivenPropertyOwnedBy(OwnerId);
        var notStarted = await _sut.SetPropertyPublishedAsync(PropertyId, true, OwnerId);

        Assert.Equal(ResponseMessages.KycRequiredToPublish, inReview.Message);
        Assert.Equal(ResponseMessages.KycNotSubmitted, notStarted.Message);
        Assert.NotEqual(inReview.Message, notStarted.Message);
    }

    [Fact]
    public async Task Unpublish_ByUnverifiedOwner_IsAllowed()
    {
        // Taking your own listing down must never be blocked. Gating this would trap
        // an owner whose verification lapsed with a live listing they cannot remove —
        // turning a trust control into a liability.
        GivenCaller(OwnerId, kycVerified: false);
        var property = GivenPropertyOwnedBy(OwnerId, published: true);

        var result = await _sut.SetPropertyPublishedAsync(PropertyId, isPublished: false, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.False(property.IsPublished);
    }

    // ── Ownership ────────────────────────────────────────────────

    [Fact]
    public async Task Publish_ByAVerifiedStrangerWhoDoesNotOwnTheListing_IsRefused()
    {
        // Being verified is not the same as being entitled. A verified agent must not
        // be able to publish somebody else's draft.
        GivenCaller(StrangerId);
        var property = GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.SetPropertyPublishedAsync(PropertyId, isPublished: true, StrangerId);

        Assert.False(result.IsSuccessful);
        Assert.False(property.IsPublished);
        AssertNothingWasPersisted();
    }

    [Fact]
    public async Task Unpublish_ByAStranger_IsRefused()
    {
        // The mirror of the above: taking someone else's listing down is vandalism.
        GivenCaller(StrangerId);
        var property = GivenPropertyOwnedBy(OwnerId, published: true);

        var result = await _sut.SetPropertyPublishedAsync(PropertyId, isPublished: false, StrangerId);

        Assert.False(result.IsSuccessful);
        Assert.True(property.IsPublished);
        AssertNothingWasPersisted();
    }

    // ── Account type ─────────────────────────────────────────────

    [Theory]
    [InlineData(CustomerType.Customer)]
    [InlineData(CustomerType.Unset)]
    public async Task Publish_ByAnAccountTypeThatCannotManageProperties_IsRefused(CustomerType type)
    {
        // Unset matters specifically: Google sign-ups land there before onboarding,
        // so it is a real state a live account can be in, not a theoretical one.
        GivenCaller(OwnerId, type);
        GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.SetPropertyPublishedAsync(PropertyId, isPublished: true, OwnerId);

        Assert.False(result.IsSuccessful);
        AssertNothingWasPersisted();
    }

    [Theory]
    [InlineData(CustomerType.HouseOwner)]
    [InlineData(CustomerType.Agent)]
    [InlineData(CustomerType.Developer)]
    public async Task Publish_ByEveryAccountTypeThatManagesProperties_Succeeds(CustomerType type)
    {
        // Guards the other direction. Developer was added to the enum after
        // CanManageProperties() was written and is easy to leave out of the set.
        GivenCaller(OwnerId, type);
        GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.SetPropertyPublishedAsync(PropertyId, isPublished: true, OwnerId);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Publish_ByAnAccountThatNoLongerExists_IsRefused()
    {
        _unitOfWork
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync((Customer?)null);
        GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.SetPropertyPublishedAsync(PropertyId, isPublished: true, OwnerId);

        Assert.False(result.IsSuccessful);
        AssertNothingWasPersisted();
    }

    // ── Ordering ─────────────────────────────────────────────────

    [Fact]
    public async Task Publish_ChecksTheCallerBeforeLoadingTheProperty()
    {
        // An unauthorised caller should not be able to confirm a property id exists by
        // watching which error comes back. Asserting the property was never read keeps
        // the cheap identity check in front of the lookup.
        GivenCaller(StrangerId, CustomerType.Customer);

        var result = await _sut.SetPropertyPublishedAsync(Guid.NewGuid(), isPublished: true, StrangerId);

        Assert.False(result.IsSuccessful);
        _unitOfWork.Verify(
            u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<PropertyEntity, bool>>>()),
            Times.Never);
    }
}
