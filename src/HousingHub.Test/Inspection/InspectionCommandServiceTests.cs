using Amazon.DynamoDBv2.DataModel;
using Mapster;
using HousingHub.Service.AdminService;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Core;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using AdminEntity = HousingHub.Model.Entities.Admin;
using HousingHub.Model.Enums;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Chat;
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
    private readonly Mock<IAdminAuthService> _adminAuthServiceMock;
    private readonly Mock<IRealtimeNotifier> _realtimeNotifierMock;
    private readonly Mock<IChatRealtimeNotifier> _chatRealtimeNotifierMock;
    private readonly Mock<IDynamoDBContext> _dynamoDbMock;
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
        _adminAuthServiceMock = new Mock<IAdminAuthService>();
        _realtimeNotifierMock = new Mock<IRealtimeNotifier>();
        _chatRealtimeNotifierMock = new Mock<IChatRealtimeNotifier>();
        _dynamoDbMock = new Mock<IDynamoDBContext>();
        var logger = NullLogger<InspectionCommandService>.Instance;

        var config = new TypeAdapterConfig();
        new InspectionMapper().Register(config);
        _mapper = new ObjectMapper(config);

        _unitOfWorkMock.Setup(u => u.PropertyInspectionCommands.InsertAsync(It.IsAny<PropertyInspection>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.PropertyInspectionCommands.UpdateAsync(It.IsAny<PropertyInspection>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        _emailServiceMock.Setup(e => e.SendInspectionScheduledAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string?>())).ReturnsAsync(true);
        _emailServiceMock.Setup(e => e.SendInspectionBookingConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string?>())).ReturnsAsync(true);
        _emailServiceMock.Setup(e => e.SendInspectionResponseAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);
        _emailServiceMock.Setup(e => e.SendInspectionHandoffToAdminsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);
        _emailServiceMock.Setup(e => e.SendStaffAssignedToInspectionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);

        _adminAuthServiceMock.Setup(a => a.GetAllStaffAsync()).ReturnsAsync(new List<AdminStaffDto>());
        _dynamoDbMock
            .Setup(d => d.LoadAsync<AdminEntity>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminEntity?)null);

        _sut = new InspectionCommandService(
            _unitOfWorkMock.Object, _mapper, _emailServiceMock.Object, _adminAuthServiceMock.Object,
            _realtimeNotifierMock.Object, _chatRealtimeNotifierMock.Object, _dynamoDbMock.Object, logger);
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
        Assert.Equal(ResponseMessages.SetNotFoundMessage("customer"), result.Message);
    }

    [Fact]
    public async Task ScheduleInspection_WhenPropertyNotFound_ReturnsFailure()
    {
        SetupCustomerLookupSequence(CreateCustomer(CustomerId));
        SetupPropertyLookup(null);

        var dto = new ScheduleInspectionDto(PropertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), null);
        var result = await _sut.ScheduleInspectionAsync(dto, CustomerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.SetNotFoundMessage("property"), result.Message);
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
    public async Task ScheduleInspection_WhenPendingRequestExistsForSameProperty_ReturnsFailure()
    {
        SetupCustomerLookupSequence(CreateCustomer(CustomerId));
        SetupPropertyLookup(CreateProperty());
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.AnyAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>())).ReturnsAsync(true);

        var dto = new ScheduleInspectionDto(PropertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), null);
        var result = await _sut.ScheduleInspectionAsync(dto, CustomerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionAlreadyPending, result.Message);
        _unitOfWorkMock.Verify(u => u.PropertyInspectionCommands.InsertAsync(It.IsAny<PropertyInspection>()), Times.Never);
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

        _unitOfWorkMock.Verify(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>()), Times.Exactly(2));
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(OwnerId, It.IsAny<Service.Dtos.Notification.NotificationDto>()), Times.Once);
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(CustomerId, It.IsAny<Service.Dtos.Notification.NotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleInspection_SendsConfirmationEmailToCustomerAndOwner()
    {
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));
        SetupPropertyLookup(CreateProperty());

        var dto = new ScheduleInspectionDto(PropertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), null);
        await _sut.ScheduleInspectionAsync(dto, CustomerId);

        _emailServiceMock.Verify(e => e.SendInspectionScheduledAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string?>()), Times.Once);
        _emailServiceMock.Verify(e => e.SendInspectionBookingConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string?>()), Times.Once);
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
    public async Task RespondToInspection_Accept_PostsSystemChatMessageToBothParties()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));

        var dto = new RespondToInspectionDto(InspectionId, true, null);
        await _sut.RespondToInspectionAsync(dto, OwnerId);

        _unitOfWorkMock.Verify(u => u.ChatMessageCommands.InsertAsync(
            It.Is<ChatMessage>(m => m.SenderId == SystemSender.Id)),
            Times.Once);
        _chatRealtimeNotifierMock.Verify(
            n => n.SendMessageAsync(CustomerId, It.Is<ChatMessageDto>(d => d.IsSystemMessage && d.SenderName == SystemSender.DisplayName)),
            Times.Once);
        _chatRealtimeNotifierMock.Verify(
            n => n.SendMessageAsync(OwnerId, It.Is<ChatMessageDto>(d => d.IsSystemMessage && d.SenderName == SystemSender.DisplayName)),
            Times.Once);
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

    [Fact]
    public async Task RespondToInspection_AsAdmin_BypassesOwnerCheckAndSucceeds()
    {
        var adminId = Guid.NewGuid();
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));

        var dto = new RespondToInspectionDto(InspectionId, true, null);
        var result = await _sut.RespondToInspectionAsync(dto, adminId, isAdminAction: true);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Confirmed, inspection.Status);
        _chatRealtimeNotifierMock.Verify(
            n => n.SendMessageAsync(OwnerId, It.Is<ChatMessageDto>(d => d.IsSystemMessage)),
            Times.Once);
    }

    // ── HandOffToHousingHubAsync ──────────────────────────────────

    [Fact]
    public async Task HandOff_AsOwner_SetsHandedOffAtAndEmailsActiveSuperAdmins()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(OwnerId)).ReturnsAsync(CreateCustomer(OwnerId, "Owner", "User"));

        var activeSuperAdmin = new AdminStaffDto(Guid.NewGuid(), "Ada", "Min", "ada@test.com", DateTime.UtcNow, true, AdminRoles.SuperAdmin);
        var inactiveSuperAdmin = new AdminStaffDto(Guid.NewGuid(), "Old", "Admin", "old@test.com", DateTime.UtcNow, false, AdminRoles.SuperAdmin);
        var staffMember = new AdminStaffDto(Guid.NewGuid(), "Jane", "Staff", "jane@test.com", DateTime.UtcNow, true, AdminRoles.Admin);
        _adminAuthServiceMock.Setup(a => a.GetAllStaffAsync())
            .ReturnsAsync(new List<AdminStaffDto> { activeSuperAdmin, inactiveSuperAdmin, staffMember });

        var result = await _sut.HandOffToHousingHubAsync(InspectionId, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(inspection.HandedOffAt);
        _emailServiceMock.Verify(e => e.SendInspectionHandoffToAdminsAsync(
            "ada@test.com", "Ada", "Owner User", "Test Property", It.IsAny<DateTime>(), It.IsAny<TimeSpan>()), Times.Once);
        _emailServiceMock.Verify(e => e.SendInspectionHandoffToAdminsAsync(
            "old@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>()), Times.Never);
        _emailServiceMock.Verify(e => e.SendInspectionHandoffToAdminsAsync(
            "jane@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task HandOff_WhenNotOwner_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

        var result = await _sut.HandOffToHousingHubAsync(InspectionId, Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionNotOwner, result.Message);
        Assert.Null(inspection.HandedOffAt);
    }

    [Fact]
    public async Task HandOff_WhenAlreadyHandedOff_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        inspection.HandedOffAt = DateTime.UtcNow.AddDays(-1);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

        var result = await _sut.HandOffToHousingHubAsync(InspectionId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionAlreadyHandedOff, result.Message);
    }

    [Fact]
    public async Task HandOff_WhenCancelled_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Cancelled);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

        var result = await _sut.HandOffToHousingHubAsync(InspectionId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionCannotHandOff, result.Message);
    }

    // ── AssignInspectionToStaffAsync ──────────────────────────────

    [Fact]
    public async Task Assign_WhenHandedOff_SetsAssignedStaffAndEmailsAssignee()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        inspection.HandedOffAt = DateTime.UtcNow;
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(OwnerId)).ReturnsAsync(CreateCustomer(OwnerId, "Owner", "User"));

        var staffId = Guid.NewGuid();
        var staffMember = new AdminStaffDto(staffId, "Jane", "Staff", "jane@test.com", DateTime.UtcNow, true, AdminRoles.Admin);
        _adminAuthServiceMock.Setup(a => a.GetAllStaffAsync()).ReturnsAsync(new List<AdminStaffDto> { staffMember });

        var result = await _sut.AssignInspectionToStaffAsync(InspectionId, staffId, Guid.NewGuid());

        Assert.True(result.IsSuccessful);
        Assert.Equal(staffId, inspection.AssignedStaffId);
        _emailServiceMock.Verify(e => e.SendStaffAssignedToInspectionAsync(
            "jane@test.com", "Jane", "Test Property", "Owner User", It.IsAny<DateTime>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public async Task Assign_WhenNotHandedOff_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);

        var result = await _sut.AssignInspectionToStaffAsync(InspectionId, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionNotHandedOff, result.Message);
        Assert.Null(inspection.AssignedStaffId);
    }

    [Fact]
    public async Task Assign_WhenStaffNotFound_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        inspection.HandedOffAt = DateTime.UtcNow;
        SetupInspectionLookup(inspection);
        _adminAuthServiceMock.Setup(a => a.GetAllStaffAsync()).ReturnsAsync(new List<AdminStaffDto>());

        var result = await _sut.AssignInspectionToStaffAsync(InspectionId, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(inspection.AssignedStaffId);
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

    [Fact]
    public async Task RescheduleInspection_AsAdmin_BypassesParticipantCheckAndNotifiesBothParties()
    {
        var adminId = Guid.NewGuid();
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId, "Jane", "Doe"), CreateCustomer(OwnerId, "Owner", "User"));
        _dynamoDbMock
            .Setup(d => d.LoadAsync<AdminEntity>(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminEntity { Id = adminId, FirstName = "Ada", LastName = "Min", Email = "ada@test.com" });

        var dto = new RescheduleInspectionDto(InspectionId, DateTime.UtcNow.AddDays(14), TimeSpan.FromHours(14), "Staff rescheduled");
        var result = await _sut.RescheduleInspectionAsync(dto, adminId, isAdminAction: true);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Rescheduled, inspection.Status);
        _emailServiceMock.Verify(e => e.SendInspectionResponseAsync(
            "jane@test.com", "Jane", "Admin - Ada Min", "Test Property", "Rescheduled",
            It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<TimeSpan?>()), Times.Once);
        _emailServiceMock.Verify(e => e.SendInspectionResponseAsync(
            "owner@test.com", "Owner", "Admin - Ada Min", "Test Property", "Rescheduled",
            It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<TimeSpan?>()), Times.Once);
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
    public async Task RespondToReschedule_Decline_RevertsToConfirmedWithNote()
    {
        var originalDate = DateTime.UtcNow.AddDays(7);
        var originalTime = TimeSpan.FromHours(10);
        var inspection = CreateInspection(status: InspectionStatus.Rescheduled);
        inspection.ScheduledDate = originalDate;
        inspection.ScheduledTime = originalTime;
        inspection.RescheduledDate = DateTime.UtcNow.AddDays(14);
        inspection.RescheduledTime = TimeSpan.FromHours(14);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));

        var result = await _sut.RespondToRescheduleAsync(InspectionId, false, CustomerId, "Doesn't work for me");

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Confirmed, inspection.Status);
        Assert.Equal(originalDate, inspection.ScheduledDate);
        Assert.Equal(originalTime, inspection.ScheduledTime);
        Assert.Equal("Doesn't work for me", inspection.DeclineNote);
    }

    [Fact]
    public async Task RespondToReschedule_ByOwner_Succeeds()
    {
        var rescheduledDate = DateTime.UtcNow.AddDays(14);
        var rescheduledTime = TimeSpan.FromHours(14);
        var inspection = CreateInspection(status: InspectionStatus.Rescheduled);
        inspection.RescheduledDate = rescheduledDate;
        inspection.RescheduledTime = rescheduledTime;
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(OwnerId, "Owner", "User"), CreateCustomer(CustomerId));

        var result = await _sut.RespondToRescheduleAsync(InspectionId, true, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Confirmed, inspection.Status);
        Assert.Equal(rescheduledDate, inspection.ScheduledDate);
    }

    [Fact]
    public async Task RespondToReschedule_WhenNotParticipant_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Rescheduled);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

        var result = await _sut.RespondToRescheduleAsync(InspectionId, true, Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InspectionNotParticipant, result.Message);
    }

    [Fact]
    public async Task RespondToReschedule_WhenNotRescheduled_ReturnsFailure()
    {
        var inspection = CreateInspection(status: InspectionStatus.Pending);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));

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

    [Fact]
    public async Task CancelInspection_AsAdmin_BypassesCustomerCheckAndSucceeds()
    {
        var adminId = Guid.NewGuid();
        var inspection = CreateInspection(status: InspectionStatus.Confirmed);
        SetupInspectionLookup(inspection);
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId));

        var result = await _sut.CancelInspectionAsync(InspectionId, adminId, isAdminAction: true);

        Assert.True(result.IsSuccessful);
        Assert.Equal(InspectionStatus.Cancelled, inspection.Status);
    }

    // ── SendDueInspectionRemindersAsync ───────────────────────────

    private PropertyInspection CreateConfirmedInspection(DateTime scheduledAt, DateTime? reminderSentAt = null)
    {
        var inspection = CreateInspection(status: InspectionStatus.Confirmed);
        inspection.ScheduledDate = scheduledAt.Date;
        inspection.ScheduledTime = scheduledAt.TimeOfDay;
        inspection.ReminderSentAt = reminderSentAt;
        return inspection;
    }

    [Fact]
    public async Task SendDueReminders_WhenDueWithin24Hours_SendsEmailAndChatToBothParties()
    {
        var inspection = CreateConfirmedInspection(DateTime.UtcNow.AddHours(20));
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetAllAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new[] { inspection });
        SetupPropertyLookup(CreateProperty(ownerId: OwnerId));
        SetupCustomerLookupSequence(CreateCustomer(CustomerId), CreateCustomer(OwnerId, "Owner", "User"));

        var result = await _sut.SendDueInspectionRemindersAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data);
        Assert.NotNull(inspection.ReminderSentAt);

        _emailServiceMock.Verify(e => e.SendInspectionReminderAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>()),
            Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.ChatMessageCommands.InsertAsync(
            It.Is<ChatMessage>(m => m.SenderId == SystemSender.Id)), Times.Once);
        _chatRealtimeNotifierMock.Verify(n => n.SendMessageAsync(CustomerId, It.Is<ChatMessageDto>(d => d.IsSystemMessage)), Times.Once);
        _chatRealtimeNotifierMock.Verify(n => n.SendMessageAsync(OwnerId, It.Is<ChatMessageDto>(d => d.IsSystemMessage)), Times.Once);
    }

    [Fact]
    public async Task SendDueReminders_WhenAlreadyReminded_SkipsIt()
    {
        var inspection = CreateConfirmedInspection(DateTime.UtcNow.AddHours(20), reminderSentAt: DateTime.UtcNow.AddMinutes(-5));
        // The real repository predicate excludes already-reminded inspections —
        // simulate that by returning an empty result, since the mock doesn't
        // evaluate the predicate itself.
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetAllAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<PropertyInspection>());

        var result = await _sut.SendDueInspectionRemindersAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Data);
        _unitOfWorkMock.Verify(u => u.ChatMessageCommands.InsertAsync(It.IsAny<ChatMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendDueReminders_WhenNotDueYet_SkipsIt()
    {
        var inspection = CreateConfirmedInspection(DateTime.UtcNow.AddHours(48));
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetAllAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new[] { inspection });

        var result = await _sut.SendDueInspectionRemindersAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Data);
        Assert.Null(inspection.ReminderSentAt);
        _unitOfWorkMock.Verify(u => u.ChatMessageCommands.InsertAsync(It.IsAny<ChatMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendDueReminders_WhenAlreadyPast_SkipsIt()
    {
        var inspection = CreateConfirmedInspection(DateTime.UtcNow.AddHours(-1));
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetAllAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new[] { inspection });

        var result = await _sut.SendDueInspectionRemindersAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Data);
        _unitOfWorkMock.Verify(u => u.ChatMessageCommands.InsertAsync(It.IsAny<ChatMessage>()), Times.Never);
    }
}
