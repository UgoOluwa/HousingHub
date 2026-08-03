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
using System.Linq.Expressions;

namespace HousingHub.Test.Admin;

public class AdminPropertyCommandServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IPropertyAlertPreferenceQueryService> _propertyAlertPreferenceQueryServiceMock;
    private readonly Mock<IRealtimeNotifier> _realtimeNotifierMock;
    private readonly PropertyCommandService _sut;

    public AdminPropertyCommandServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var config = new TypeAdapterConfig();
        new PropertyMapper().Register(config);
        var mapper = new ObjectMapper(config);
        var fileStorage = new Mock<IFileStorageService>();
        var geocodingService = new Mock<IGeocodingService>();
        _emailServiceMock = new Mock<IEmailService>();
        _propertyAlertPreferenceQueryServiceMock = new Mock<IPropertyAlertPreferenceQueryService>();
        _realtimeNotifierMock = new Mock<IRealtimeNotifier>();

        // The tests in this file exercise the admin-moderation paths (publish/verify/delete),
        // not the saved-search alert hook itself — default to "no active preferences" so the
        // publish flow's best-effort notification step is a no-op unless a test opts in.
        _propertyAlertPreferenceQueryServiceMock
            .Setup(p => p.GetAllActiveAsync())
            .ReturnsAsync(new List<PropertyAlertPreference>());

        _sut = new PropertyCommandService(
            NullLogger<PropertyCommandService>.Instance,
            _unitOfWorkMock.Object,
            mapper,
            fileStorage.Object,
            geocodingService.Object,
            _emailServiceMock.Object,
            _propertyAlertPreferenceQueryServiceMock.Object,
            _realtimeNotifierMock.Object);
    }

    private static Property MakeProperty(bool isPublished = false) => new("Title", "Desc",
        PropertyType.Apartment, 100_000m, PropertyAvailability.Available, PropertyLeaseType.Rent)
    {
        Id = Guid.NewGuid(),
        IsPublished = isPublished,
        DateCreated = DateTime.UtcNow,
        DateModified = DateTime.UtcNow
    };

    private static Customer MakeOwner(Guid ownerId) => new("Jane", "Smith", "jane@test.com", "08011112222", CustomerType.HouseOwner, "hash")
    {
        Id = ownerId
    };

    // ── SetPropertyPublishedAsync ─────────────────────────────────────────────

    [Fact]
    public async Task SetPropertyPublished_Publish_SetsIsPublishedAndPublishedAt()
    {
        var property = MakeProperty(false);
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<Property>())).Returns(Task.CompletedTask);

        var result = await _sut.SetPropertyPublishedAsync(property.Id, true);

        Assert.True(result.IsSuccessful);
        Assert.True(property.IsPublished);
        Assert.NotNull(property.PublishedAt);
    }

    [Fact]
    public async Task SetPropertyPublished_Unpublish_ClearsPublishedAt()
    {
        var property = MakeProperty(true);
        property.PublishedAt = DateTime.UtcNow.AddDays(-1);

        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<Property>())).Returns(Task.CompletedTask);

        var result = await _sut.SetPropertyPublishedAsync(property.Id, false);

        Assert.True(result.IsSuccessful);
        Assert.False(property.IsPublished);
        Assert.Null(property.PublishedAt);
    }

    [Fact]
    public async Task SetPropertyPublished_Unpublish_PersistsReasonAndEmailsOwner()
    {
        var property = MakeProperty(true);
        var owner = MakeOwner(property.OwnerId);

        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<Property>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(property.OwnerId)).ReturnsAsync(owner);

        var result = await _sut.SetPropertyPublishedAsync(property.Id, false, "Listing violates policy");

        Assert.True(result.IsSuccessful);
        Assert.Equal("Listing violates policy", property.UnpublishReason);
        _emailServiceMock.Verify(e => e.SendPropertyUnpublishedAsync(owner.Email, "Jane Smith", property.Title, "Listing violates policy"), Times.Once);
    }

    [Fact]
    public async Task SetPropertyPublished_Publish_ClearsUnpublishReasonAndDoesNotEmail()
    {
        var property = MakeProperty(false);
        property.UnpublishReason = "Old reason";

        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<Property>())).Returns(Task.CompletedTask);

        var result = await _sut.SetPropertyPublishedAsync(property.Id, true);

        Assert.True(result.IsSuccessful);
        Assert.Null(property.UnpublishReason);
        _emailServiceMock.Verify(e => e.SendPropertyUnpublishedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── SetPropertyVerifiedAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SetPropertyVerified_Verify_EmailsOwner()
    {
        var property = MakeProperty();
        var owner = MakeOwner(property.OwnerId);

        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<Property>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(property.OwnerId)).ReturnsAsync(owner);

        var result = await _sut.SetPropertyVerifiedAsync(property.Id, true);

        Assert.True(result.IsSuccessful);
        Assert.True(property.IsVerified);
        _emailServiceMock.Verify(e => e.SendPropertyVerifiedAsync(owner.Email, "Jane Smith", property.Title), Times.Once);
    }

    [Fact]
    public async Task SetPropertyVerified_Unverify_DoesNotEmail()
    {
        var property = MakeProperty();
        property.IsVerified = true;

        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<Property>())).Returns(Task.CompletedTask);

        var result = await _sut.SetPropertyVerifiedAsync(property.Id, false);

        Assert.True(result.IsSuccessful);
        Assert.False(property.IsVerified);
        _emailServiceMock.Verify(e => e.SendPropertyVerifiedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SetPropertyPublished_NotFound_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync((Property?)null);

        var result = await _sut.SetPropertyPublishedAsync(Guid.NewGuid(), true);

        Assert.False(result.IsSuccessful);
    }

    // ── AdminDeletePropertyAsync ───────────────────────────────────────────────

    [Fact]
    public async Task AdminDeleteProperty_ExistingProperty_ReturnsSuccess()
    {
        var property = MakeProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.DeleteAsync(It.IsAny<Property>())).Returns(Task.CompletedTask);

        var result = await _sut.AdminDeletePropertyAsync(property.Id, "Test reason");

        Assert.True(result.IsSuccessful);
        _unitOfWorkMock.Verify(u => u.PropertyCommands.DeleteAsync(property), Times.Once);
    }

    [Fact]
    public async Task AdminDeleteProperty_EmailsOwnerWithReason()
    {
        var property = MakeProperty();
        var owner = MakeOwner(property.OwnerId);

        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.DeleteAsync(It.IsAny<Property>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(property.OwnerId)).ReturnsAsync(owner);

        var result = await _sut.AdminDeletePropertyAsync(property.Id, "Duplicate listing");

        Assert.True(result.IsSuccessful);
        _emailServiceMock.Verify(e => e.SendPropertyDeletedAsync(owner.Email, "Jane Smith", property.Title, "Duplicate listing"), Times.Once);
    }

    [Fact]
    public async Task AdminDeleteProperty_NotFound_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync((Property?)null);

        var result = await _sut.AdminDeletePropertyAsync(Guid.NewGuid(), "Test reason");

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task AdminDeleteProperty_RepositoryThrows_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.AdminDeletePropertyAsync(Guid.NewGuid(), "Test reason");

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task SetPropertyPublished_RepositoryThrows_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.SetPropertyPublishedAsync(Guid.NewGuid(), true);

        Assert.False(result.IsSuccessful);
    }
}
