using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.NotificationService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Notifications;

public class NotificationQueryServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly IMapper _mapper;
    private readonly NotificationQueryService _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public NotificationQueryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var logger = NullLogger<NotificationQueryService>.Instance;

        var config = new MapperConfiguration(cfg => cfg.AddProfile<InspectionMapper>(), NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _sut = new NotificationQueryService(_unitOfWorkMock.Object, _mapper, logger);
    }

    private static HousingHub.Model.Entities.Notification CreateNotification(
        Guid? id = null, Guid? recipientId = null, bool isRead = false) => new()
    {
        Id = id ?? Guid.NewGuid(),
        RecipientId = recipientId ?? UserId,
        InspectionId = Guid.NewGuid(),
        Type = NotificationType.InspectionScheduled,
        Title = "Test Notification",
        Message = "Test message",
        IsRead = isRead,
        DateCreated = DateTime.UtcNow
    };

    // ── GetNotificationsAsync ────────────────────────────────────

    [Fact]
    public async Task GetNotificationsAsync_ReturnsPagedResults()
    {
        var notifications = new List<HousingHub.Model.Entities.Notification>
        {
            CreateNotification(),
            CreateNotification()
        };

        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync((notifications.AsEnumerable(), 2));

        var result = await _sut.GetNotificationsAsync(UserId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
    }

    [Fact]
    public async Task GetNotificationsAsync_WithUnreadFilter_PassesPredicate()
    {
        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync((Enumerable.Empty<HousingHub.Model.Entities.Notification>(), 0));

        var result = await _sut.GetNotificationsAsync(UserId, 1, 10, unreadOnly: true);

        Assert.True(result.IsSuccessful);
        _unitOfWorkMock.Verify(u => u.NotificationQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()), Times.Once);
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenEmpty_ReturnsEmptyResult()
    {
        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync((Enumerable.Empty<HousingHub.Model.Entities.Notification>(), 0));

        var result = await _sut.GetNotificationsAsync(UserId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetNotificationsAsync_MapsFieldsCorrectly()
    {
        var notification = CreateNotification();
        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync((new[] { notification }.AsEnumerable(), 1));

        var result = await _sut.GetNotificationsAsync(UserId, 1, 10);

        Assert.Equal("Test Notification", result.Data!.Items[0].Title);
        Assert.Equal("Test message", result.Data.Items[0].Message);
    }

    // ── GetUnreadCountAsync ──────────────────────────────────────

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCount()
    {
        _unitOfWorkMock.Setup(u => u.NotificationQueries.CountAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync(5);

        var result = await _sut.GetUnreadCountAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(5, result.Data);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WithNoUnread_ReturnsZero()
    {
        _unitOfWorkMock.Setup(u => u.NotificationQueries.CountAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync(0);

        var result = await _sut.GetUnreadCountAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Data);
    }
}
