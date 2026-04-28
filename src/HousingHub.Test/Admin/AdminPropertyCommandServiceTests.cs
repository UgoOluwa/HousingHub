using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.PropertyService;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Admin;

public class AdminPropertyCommandServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly PropertyCommandService _sut;

    public AdminPropertyCommandServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var config = new TypeAdapterConfig();
        new PropertyMapper().Register(config);
        var mapper = new ObjectMapper(config);
        var fileStorage = new Mock<IFileStorageService>();
        _sut = new PropertyCommandService(
            NullLogger<PropertyCommandService>.Instance,
            _unitOfWorkMock.Object,
            mapper,
            fileStorage.Object);
    }

    private static Property MakeProperty(bool isPublished = false) => new("Title", "Desc",
        PropertyType.Apartment, 100_000m, PropertyAvailability.Available, PropertyLeaseType.Rent)
    {
        Id = Guid.NewGuid(),
        IsPublished = isPublished,
        DateCreated = DateTime.UtcNow,
        DateModified = DateTime.UtcNow
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

        var result = await _sut.AdminDeletePropertyAsync(property.Id);

        Assert.True(result.IsSuccessful);
        _unitOfWorkMock.Verify(u => u.PropertyCommands.DeleteAsync(property), Times.Once);
    }

    [Fact]
    public async Task AdminDeleteProperty_NotFound_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync((Property?)null);

        var result = await _sut.AdminDeletePropertyAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task AdminDeleteProperty_RepositoryThrows_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.AdminDeletePropertyAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }
}
