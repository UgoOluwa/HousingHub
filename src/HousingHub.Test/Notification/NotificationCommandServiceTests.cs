using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.NotificationService;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Notifications;

public class NotificationCommandServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly NotificationCommandService _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public NotificationCommandServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var logger = NullLogger<NotificationCommandService>.Instance;

        _unitOfWorkMock.Setup(u => u.NotificationCommands.UpdateAsync(It.IsAny<HousingHub.Model.Entities.Notification>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.NotificationCommands.UpdateRangeAsync(It.IsAny<IEnumerable<HousingHub.Model.Entities.Notification>>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        _sut = new NotificationCommandService(_unitOfWorkMock.Object, logger);
    }

    private static HousingHub.Model.Entities.Notification CreateNotification(
        Guid? id = null, Guid? recipientId = null, bool isRead = false) => new()
    {
        Id = id ?? Guid.NewGuid(),
        RecipientId = recipientId ?? UserId,
        InspectionId = Guid.NewGuid(),
        Type = NotificationType.InspectionScheduled,
        Title = "Test",
        Message = "Test message",
        IsRead = isRead,
        DateCreated = DateTime.UtcNow
    };

    // ── MarkAsReadAsync ──────────────────────────────────────────

    [Fact]
    public async Task MarkAsReadAsync_WithOwnNotification_ReturnsSuccess()
    {
        var notificationId = Guid.NewGuid();
        var notification = CreateNotification(id: notificationId, recipientId: UserId);
        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync(notification);

        var result = await _sut.MarkAsReadAsync(notificationId, UserId);

        Assert.True(result.IsSuccessful);
        Assert.True(notification.IsRead);
    }

    [Fact]
    public async Task MarkAsReadAsync_WithNonExistentNotification_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync((HousingHub.Model.Entities.Notification?)null);

        var result = await _sut.MarkAsReadAsync(Guid.NewGuid(), UserId);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task MarkAsReadAsync_WithOtherUsersNotification_ReturnsFailure()
    {
        var otherUserId = Guid.NewGuid();
        var notification = CreateNotification(recipientId: otherUserId);
        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync(notification);

        var result = await _sut.MarkAsReadAsync(notification.Id, UserId);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task MarkAsReadAsync_CallsSaveAsync()
    {
        var notification = CreateNotification(recipientId: UserId);
        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync(notification);

        await _sut.MarkAsReadAsync(notification.Id, UserId);

        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    // ── MarkAllAsReadAsync ───────────────────────────────────────

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllUnreadAsRead()
    {
        var notifications = new List<HousingHub.Model.Entities.Notification>
        {
            CreateNotification(recipientId: UserId, isRead: false),
            CreateNotification(recipientId: UserId, isRead: false)
        };

        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetAllAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync(notifications);

        var result = await _sut.MarkAllAsReadAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.True(notifications.All(n => n.IsRead));
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WhenNoUnread_StillReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetAllAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.Notification>());

        var result = await _sut.MarkAllAsReadAsync(UserId);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_CallsUpdateRangeAndSave()
    {
        var notifications = new List<HousingHub.Model.Entities.Notification>
        {
            CreateNotification(recipientId: UserId, isRead: false)
        };

        _unitOfWorkMock.Setup(u => u.NotificationQueries.GetAllAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.Notification, bool>>>()))
            .ReturnsAsync(notifications);

        await _sut.MarkAllAsReadAsync(UserId);

        _unitOfWorkMock.Verify(u => u.NotificationCommands.UpdateRangeAsync(It.IsAny<IEnumerable<HousingHub.Model.Entities.Notification>>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }
}
