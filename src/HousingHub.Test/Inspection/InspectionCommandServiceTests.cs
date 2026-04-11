using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService;
using HousingHub.Service.NotificationService.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Inspection;

public class InspectionCommandServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IRealtimeNotifier> _realtimeNotifierMock;
    private readonly IMapper _mapper;
    private readonly InspectionCommandService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid InspectionId = Guid.NewGuid();

    public InspectionCommandServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _emailServiceMock = new Mock<IEmailService>();
        _realtimeNotifierMock = new Mock<IRealtimeNotifier>();
        var logger = NullLogger<InspectionCommandService>.Instance;

        var config = new MapperConfiguration(cfg => cfg.AddProfile<InspectionMapper>(), NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _unitOfWorkMock.Setup(u => u.PropertyInspectionCommands.InsertAsync(It.IsAny<PropertyInspection>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.PropertyInspectionCommands.UpdateAsync(It.IsAny<PropertyInspection>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        _emailServiceMock.Setup(e => e.SendInspectionScheduledAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string?>())).ReturnsAsync(true);
        _emailServiceMock.Setup(e => e.SendInspectionResponseAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        _sut = new InspectionCommandService(_unitOfWorkMock.Object, _mapper, _emailServiceMock.Object, _realtimeNotifierMock.Object, logger);
    }

    private static Customer CreateCustomer(Guid id, string firstName = "Test", string lastName = "User") =>
        new(firstName, lastName, $"{firstName.ToLower()}@test.com", "08012345678", CustomerType.Customer, "hash")
        {
            Id = id
        };

    private static Property CreateProperty(Guid? id = null, Guid? ownerId = null) => new()
    {
        Id = id ?? PropertyId,
        Title = "Test Property",
        OwnerId = ownerId ?? OwnerId,
        Latitude = 6.5,
        Longitude = 3.3
    };

    private static PropertyInspection CreateInspection(
        Guid? id = null, Guid? customerId = null, Guid? propertyId = null,
        InspectionStatus status = InspectionStatus.Pending) =>
        new(customerId ?? CustomerId, propertyId ?? PropertyId,
            DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), "Test note")
        {
            Id = id ?? InspectionId,
            Status = status
        };

    private void SetupCustomerLookup(Guid id, Customer? customer)
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.Is<Expression<Func<Customer, bool>>>(e => true)))
            .ReturnsAsync(customer);
    }

    private void SetupCustomerLookupSequence(params Customer?[] customers)
    {
        var setup = _unitOfWorkMock.SetupSequence(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()));
        foreach (var c in customers)
            setup = setup.ReturnsAsync(c);
    }

    private void SetupPropertyLookup(Property? property)
    {
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(property);
    }

    private void SetupInspectionLookup(PropertyInspection? inspection)
    {
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetByAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(inspection);
    }

    // ── ScheduleInspectionAsync ──────────────────────────────────

    [Fact]
    public async Task ScheduleInspection_WithValidData_ReturnsSuccess()
    {
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));
        SetupPropertyLookup(CreateProperty());

        var dto = new ScheduleInspectionDto(PropertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), "Please confirm");
        var result = await _sut.ScheduleInspectionAsync(dto, CustomerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task ScheduleInspection_WhenCustomerNotFound_ReturnsFailure()
    {
        SetupCustomerLookupSequence((Customer?)null);

        var dto = new ScheduleInspectionDto(PropertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), null);
        var result = await _sut.ScheduleInspectionAsync(dto, CustomerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task ScheduleInspection_WhenPropertyNotFound_ReturnsFailure()
    {
        SetupCustomerLookupSequence(CreateCustomer(CustomerId));
        SetupPropertyLookup(null);

        var dto = new ScheduleInspectionDto(PropertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), null);
        var result = await _sut.ScheduleInspectionAsync(dto, CustomerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task ScheduleInspection_OnOwnProperty_ReturnsFailure()
    {
        SetupCustomerLookupSequence(CreateCustomer(OwnerId));
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

        var dto = new ScheduleInspectionDto(PropertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), null);
        var result = await _sut.ScheduleInspectionAsync(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.CannotInspectOwnProperty, result.Message);
    }

    [Fact]
    public async Task ScheduleInspection_WhenInsertFails_ReturnsFailure()
    {
        SetupCustomerLookupSequence(CreateCustomer(CustomerId));
        SetupPropertyLookup(CreateProperty());
        _unitOfWorkMock.Setup(u => u.PropertyInspectionCommands.InsertAsync(It.IsAny<PropertyInspection>())).ReturnsAsync(false);

        var dto = new ScheduleInspectionDto(PropertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), null);
        var result = await _sut.ScheduleInspectionAsync(dto, CustomerId);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task ScheduleInspection_SendsNotificationToOwner()
    {
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));
        SetupPropertyLookup(CreateProperty());

        var dto = new ScheduleInspectionDto(PropertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), null);
        await _sut.ScheduleInspectionAsync(dto, CustomerId);

        _unitOfWorkMock.Verify(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>()), Times.Once);
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(OwnerId, It.IsAny<Service.Dtos.Notification.NotificationDto>()), Times.Once);
    }

    // ── RespondToInspectionAsync ─────────────────────────────────

    [Fact]
    public async Task RespondToInspection_AcceptAsPending_ReturnsSuccess()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));

        var dto = new RespondToInspectionDto(InspectionId, true, null);
        var result = await _sut.RespondToInspectionAsync(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Confirmed, inspection.Status);
    }

    [Fact]
    public async Task RespondToInspection_Decline_SetsDeclinedStatusAndNote()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));

        var dto = new RespondToInspectionDto(InspectionId, false, "Not available");
        var result = await _sut.RespondToInspectionAsync(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Declined, inspection.Status);
        Assert.Equal("Not available", inspection.DeclineNote);
    }

    [Fact]
    public async Task RespondToInspection_WhenNotOwner_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

        var dto = new RespondToInspectionDto(InspectionId, true, null);
        var result = await _sut.RespondToInspectionAsync(dto, CustomerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionNotOwner, result.Message);
    }

    [Fact]
    public async Task RespondToInspection_WhenNotPending_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Confirmed);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

        var dto = new RespondToInspectionDto(InspectionId, true, null);
        var result = await _sut.RespondToInspectionAsync(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionNotPending, result.Message);
    }

    [Fact]
    public async Task RespondToInspection_WhenNotFound_ReturnsFailure()
    {
        SetupInspectionLookup(null);

        var dto = new RespondToInspectionDto(InspectionId, true, null);
        var result = await _sut.RespondToInspectionAsync(dto, OwnerId);

        Assert.False(result.IsSuccessful);
    }

    // ── RescheduleInspectionAsync ────────────────────────────────

    [Fact]
    public async Task RescheduleInspection_AsOwner_ReturnsSuccess()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(OwnerId, "Owner", "User"), CreateCustomer(CustomerId));

        var newDate = DateTime.UtcNow.AddDays(14);
        var newTime = TimeSpan.FromHours(14);
        var dto = new RescheduleInspectionDto(InspectionId, newDate, newTime, "Owner rescheduled");
        var result = await _sut.RescheduleInspectionAsync(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Rescheduled, inspection.Status);
        Assert.Equal(newDate, inspection.RescheduledDate);
    }

    [Fact]
    public async Task RescheduleInspection_AsCustomer_ReturnsSuccess()
    {
        var inspection = CreateInspection(status: InspectionStatus.Confirmed);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));

        var dto = new RescheduleInspectionDto(InspectionId, DateTime.UtcNow.AddDays(14), TimeSpan.FromHours(14), null);
        var result = await _sut.RescheduleInspectionAsync(dto, CustomerId);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task RescheduleInspection_WhenNotParticipant_ReturnsFailure()
    {
        var nonParticipant = Guid.NewGuid();
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

        var dto = new RescheduleInspectionDto(InspectionId, DateTime.UtcNow.AddDays(14), TimeSpan.FromHours(14), null);
        var result = await _sut.RescheduleInspectionAsync(dto, nonParticipant);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionNotParticipant, result.Message);
    }

    [Fact]
    public async Task RescheduleInspection_WhenCancelled_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Cancelled);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

        var dto = new RescheduleInspectionDto(InspectionId, DateTime.UtcNow.AddDays(14), TimeSpan.FromHours(14), null);
        var result = await _sut.RescheduleInspectionAsync(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionCannotReschedule, result.Message);
    }

    // ── RespondToRescheduleAsync ─────────────────────────────────

    [Fact]
    public async Task RespondToReschedule_Accept_UpdatesSchedule()
    {
        var rescheduledDate = DateTime.UtcNow.AddDays(14);
        var rescheduledTime = TimeSpan.FromHours(14);
        var inspection = CreateInspection(status: InspectionStatus.Rescheduled);
        inspection.RescheduledDate = rescheduledDate;
        inspection.RescheduledTime = rescheduledTime;
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));

        var result = await _sut.RespondToRescheduleAsync(InspectionId, true, CustomerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Confirmed, inspection.Status);
        Assert.Equal(rescheduledDate, inspection.ScheduledDate);
    }

    [Fact]
    public async Task RespondToReschedule_Decline_CancelsInspection()
    {
        var inspection = CreateInspection(status: InspectionStatus.Rescheduled);
        inspection.RescheduledDate = DateTime.UtcNow.AddDays(14);
        inspection.RescheduledTime = TimeSpan.FromHours(14);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));

        var result = await _sut.RespondToRescheduleAsync(InspectionId, false, CustomerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Cancelled, inspection.Status);
    }

    [Fact]
    public async Task RespondToReschedule_WhenNotCustomer_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Rescheduled);
        SetupInspectionLookup(inspection);

        var result = await _sut.RespondToRescheduleAsync(InspectionId, true, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionNotCustomer, result.Message);
    }

    [Fact]
    public async Task RespondToReschedule_WhenNotRescheduled_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);

        var result = await _sut.RespondToRescheduleAsync(InspectionId, true, CustomerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionNotPendingOrRescheduled, result.Message);
    }

    // ── CancelInspectionAsync ────────────────────────────────────

    [Fact]
    public async Task CancelInspection_WithValidData_ReturnsSuccess()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId));

        var result = await _sut.CancelInspectionAsync(InspectionId, CustomerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Cancelled, inspection.Status);
    }

    [Fact]
    public async Task CancelInspection_WhenNotCustomer_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);

        var result = await _sut.CancelInspectionAsync(InspectionId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionNotCustomer, result.Message);
    }

    [Fact]
    public async Task CancelInspection_WhenAlreadyCancelled_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Cancelled);
        SetupInspectionLookup(inspection);

        var result = await _sut.CancelInspectionAsync(InspectionId, CustomerId);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task CancelInspection_WhenCompleted_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Completed);
        SetupInspectionLookup(inspection);

        var result = await _sut.CancelInspectionAsync(InspectionId, CustomerId);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task CancelInspection_WhenNotFound_ReturnsFailure()
    {
        SetupInspectionLookup(null);

        var result = await _sut.CancelInspectionAsync(InspectionId, CustomerId);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task CancelInspection_NotifiesOwner()
    {
        var inspection = CreateInspection(status: InspectionStatus.Confirmed);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId));

        await _sut.CancelInspectionAsync(InspectionId, CustomerId);

        _unitOfWorkMock.Verify(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>()), Times.Once);
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(OwnerId, It.IsAny<Service.Dtos.Notification.NotificationDto>()), Times.Once);
    }
}
