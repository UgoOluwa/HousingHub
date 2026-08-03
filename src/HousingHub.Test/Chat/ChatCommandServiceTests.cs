using Amazon.DynamoDBv2.DataModel;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using AdminEntity = HousingHub.Model.Entities.Admin;
using HousingHub.Service.ChatService;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Chat;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.NotificationService.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Chat;

public class ChatCommandServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly Mock<IChatRealtimeNotifier> _realtimeNotifierMock;
    private readonly Mock<IRealtimeNotifier> _bellNotifierMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IDynamoDBContext> _dynamoDbMock;
    private readonly ChatCommandService _sut;

    private static readonly Guid SenderId = Guid.NewGuid();
    private static readonly Guid RecipientId = Guid.NewGuid();
    private static readonly Guid ConversationId = Guid.NewGuid();

    public ChatCommandServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _realtimeNotifierMock = new Mock<IChatRealtimeNotifier>();
        _bellNotifierMock = new Mock<IRealtimeNotifier>();
        _emailServiceMock = new Mock<IEmailService>();
        var logger = NullLogger<ChatCommandService>.Instance;

        // Set up default returns for command repository methods used by the service
        _unitOfWorkMock.Setup(u => u.ConversationCommands.InsertAsync(It.IsAny<Conversation>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.ConversationCommands.UpdateAsync(It.IsAny<Conversation>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.ChatMessageCommands.InsertAsync(It.IsAny<ChatMessage>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.ChatMessageCommands.UpdateRangeAsync(It.IsAny<IEnumerable<ChatMessage>>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        _dynamoDbMock = new Mock<IDynamoDBContext>();
        _dynamoDbMock
            .Setup(d => d.LoadAsync<AdminEntity>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminEntity?)null);

        _sut = new ChatCommandService(_unitOfWorkMock.Object, logger, _realtimeNotifierMock.Object, _bellNotifierMock.Object, _emailServiceMock.Object, _dynamoDbMock.Object);
    }

    private static Customer CreateCustomer(Guid id, string firstName = "Test", string lastName = "User") =>
        new(firstName, lastName, $"{firstName.ToLower()}@test.com", "08012345678", CustomerType.Customer, "hash")
        {
            Id = id
        };

    private static Conversation CreateConversation(Guid? id = null) => new(SenderId, RecipientId)
    {
        Id = id ?? ConversationId
    };

    // ── SendMessageAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_WithValidData_ReturnsSuccess()
    {
        SetupSenderAndRecipient();
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Hello!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("Hello!", result.Data!.Content);
        Assert.Equal(SenderId, result.Data.SenderId);
    }

    [Fact]
    public async Task SendMessageAsync_ToSelf_ReturnsFailure()
    {
        var dto = new SendMessageDto(SenderId, "Hello me!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("yourself", result.Message);
    }

    [Fact]
    public async Task SendMessageAsync_SenderNotFound_ReturnsFailure()
    {
        SetupCustomerLookup(null); // sender not found

        var dto = new SendMessageDto(RecipientId, "Hello!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task SendMessageAsync_SenderIsAdmin_ResolvesNameFromAdminsTableAndSucceeds()
    {
        // Sender isn't a Customer at all (e.g. an admin messaging from the dashboard) —
        // falls back to the Admins table instead of failing as "sender not found".
        SetupCustomerLookup(null);
        _unitOfWorkMock
            .SetupSequence(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync((Customer?)null) // sender lookup misses
            .ReturnsAsync(CreateCustomer(RecipientId, "Jane", "Smith")); // recipient lookup hits
        _dynamoDbMock
            .Setup(d => d.LoadAsync<AdminEntity>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminEntity { Id = SenderId, FirstName = "Ada", LastName = "Min", Email = "ada@test.com" });
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Hello from support!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Admin - Ada Min", result.Data!.SenderName);
    }

    [Fact]
    public async Task SendMessageAsync_RecipientIsAdmin_ResolvesFromAdminsTableAndSucceeds()
    {
        // Recipient isn't a Customer at all (e.g. an owner messaging/replying to
        // an admin) — falls back to the Admins table instead of failing as
        // "recipient not found".
        var sender = CreateCustomer(SenderId, "John", "Doe");
        _unitOfWorkMock
            .SetupSequence(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(sender) // sender lookup hits
            .ReturnsAsync((Customer?)null); // recipient lookup misses among customers
        _dynamoDbMock
            .Setup(d => d.LoadAsync<AdminEntity>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminEntity { Id = RecipientId, FirstName = "Ada", LastName = "Min", Email = "ada@test.com" });
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Hi, I have a question.");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.True(result.IsSuccessful);
        _emailServiceMock.Verify(
            e => e.SendNewMessageAsync("ada@test.com", "Ada", "John Doe", "Hi, I have a question."),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_RecipientNotFound_ReturnsFailure()
    {
        // First call returns sender, second call returns null (recipient not found)
        var sender = CreateCustomer(SenderId, "John", "Doe");
        _unitOfWorkMock.SetupSequence(u => u.CustomerQueries.GetByAsync(
                It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(sender)
            .ReturnsAsync((Customer?)null);

        var dto = new SendMessageDto(RecipientId, "Hello!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task SendMessageAsync_CreatesNewConversation_WhenNoneExists()
    {
        SetupSenderAndRecipient();
        SetupConversationLookup(null); // no existing conversation
        SetupSaveAsync();

        _unitOfWorkMock
            .Setup(u => u.ConversationCommands.InsertAsync(It.IsAny<Conversation>()))
            .ReturnsAsync(true);

        var dto = new SendMessageDto(RecipientId, "First message!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.True(result.IsSuccessful);
        _unitOfWorkMock.Verify(u => u.ConversationCommands.InsertAsync(It.IsAny<Conversation>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_UsesExistingConversation_WhenOneExists()
    {
        SetupSenderAndRecipient();
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Another message!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.True(result.IsSuccessful);
        _unitOfWorkMock.Verify(u => u.ConversationCommands.InsertAsync(It.IsAny<Conversation>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageAsync_InsertsMessage()
    {
        SetupSenderAndRecipient();
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Test message");
        await _sut.SendMessageAsync(dto, SenderId);

        _unitOfWorkMock.Verify(u => u.ChatMessageCommands.InsertAsync(It.IsAny<ChatMessage>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_UpdatesConversationPreview()
    {
        SetupSenderAndRecipient();
        var conversation = CreateConversation();
        SetupConversationLookup(conversation);
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Preview text");
        await _sut.SendMessageAsync(dto, SenderId);

        Assert.Equal("Preview text", conversation.LastMessage);
        Assert.NotNull(conversation.LastMessageAt);
        _unitOfWorkMock.Verify(u => u.ConversationCommands.UpdateAsync(conversation), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_TruncatesLongMessage_InConversationPreview()
    {
        SetupSenderAndRecipient();
        var conversation = CreateConversation();
        SetupConversationLookup(conversation);
        SetupSaveAsync();

        var longContent = new string('A', 200);
        var dto = new SendMessageDto(RecipientId, longContent);
        await _sut.SendMessageAsync(dto, SenderId);

        Assert.NotNull(conversation.LastMessage);
        Assert.True(conversation.LastMessage!.Length <= 103); // 100 chars + "..."
        Assert.EndsWith("...", conversation.LastMessage);
    }

    [Fact]
    public async Task SendMessageAsync_CallsSaveAsync()
    {
        SetupSenderAndRecipient();
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Save test");
        await _sut.SendMessageAsync(dto, SenderId);

        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_PushesRealtimeMessage()
    {
        SetupSenderAndRecipient();
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Real-time!");
        await _sut.SendMessageAsync(dto, SenderId);

        _realtimeNotifierMock.Verify(
            n => n.SendMessageAsync(RecipientId, It.Is<ChatMessageDto>(m => m.Content == "Real-time!")),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_PushesConversationUpdate()
    {
        SetupSenderAndRecipient();
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Update!");
        await _sut.SendMessageAsync(dto, SenderId);

        _realtimeNotifierMock.Verify(
            n => n.NotifyConversationUpdatedAsync(RecipientId, It.IsAny<ConversationDto>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_CreatesAndPushesBellNotification()
    {
        SetupSenderAndRecipient();
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Bell test");
        await _sut.SendMessageAsync(dto, SenderId);

        _unitOfWorkMock.Verify(u => u.NotificationCommands.InsertAsync(
            It.Is<Notification>(n => n.RecipientId == RecipientId && n.Type == NotificationType.NewMessage)),
            Times.Once);
        _bellNotifierMock.Verify(
            n => n.SendNotificationAsync(RecipientId, It.Is<NotificationDto>(d => d.Type == NotificationType.NewMessage)),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsSenderName()
    {
        var sender = CreateCustomer(SenderId, "John", "Doe");
        var recipient = CreateCustomer(RecipientId, "Jane", "Smith");
        _unitOfWorkMock.SetupSequence(u => u.CustomerQueries.GetByAsync(
                It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(sender)
            .ReturnsAsync(recipient);
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Hi Jane!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("John Doe", result.Data!.SenderName);
    }

    [Fact]
    public async Task SendMessageAsync_SendsNewMessageEmailToRecipient()
    {
        SetupSenderAndRecipient();
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();

        var dto = new SendMessageDto(RecipientId, "Hi Jane, are you around?");
        await _sut.SendMessageAsync(dto, SenderId);

        _emailServiceMock.Verify(
            e => e.SendNewMessageAsync("jane@test.com", "Jane", "John Doe", "Hi Jane, are you around?"),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WhenEmailServiceThrows_StillReturnsSuccess()
    {
        // The email is a best-effort notification — a Resend outage or bad
        // recipient address must not fail the message send itself.
        SetupSenderAndRecipient();
        SetupConversationLookup(CreateConversation());
        SetupSaveAsync();
        _emailServiceMock
            .Setup(e => e.SendNewMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Resend unreachable"));

        var dto = new SendMessageDto(RecipientId, "Hello!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task SendMessageAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var dto = new SendMessageDto(RecipientId, "Hello!");
        var result = await _sut.SendMessageAsync(dto, SenderId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("DB error", result.Message);
    }

    // ── MarkConversationAsReadAsync ──────────────────────────────────────

    [Fact]
    public async Task MarkConversationAsReadAsync_WithUnreadMessages_ReturnsSuccess()
    {
        var conversation = CreateConversation();
        SetupConversationByIdLookup(conversation);
        SetupUnreadMessages(ConversationId, SenderId, 3);
        SetupSaveAsync();

        var result = await _sut.MarkConversationAsReadAsync(ConversationId, RecipientId);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_MarksMessagesAsRead()
    {
        var conversation = CreateConversation();
        SetupConversationByIdLookup(conversation);
        var messages = SetupUnreadMessages(ConversationId, SenderId, 2);
        SetupSaveAsync();

        await _sut.MarkConversationAsReadAsync(ConversationId, RecipientId);

        Assert.All(messages, m => Assert.True(m.IsRead));
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_CallsUpdateRangeAndSave()
    {
        var conversation = CreateConversation();
        SetupConversationByIdLookup(conversation);
        SetupUnreadMessages(ConversationId, SenderId, 2);
        SetupSaveAsync();

        await _sut.MarkConversationAsReadAsync(ConversationId, RecipientId);

        _unitOfWorkMock.Verify(u => u.ChatMessageCommands.UpdateRangeAsync(It.IsAny<IEnumerable<ChatMessage>>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_NotifiesOtherParticipant()
    {
        var conversation = CreateConversation();
        SetupConversationByIdLookup(conversation);
        SetupUnreadMessages(ConversationId, SenderId, 1);
        SetupSaveAsync();

        await _sut.MarkConversationAsReadAsync(ConversationId, RecipientId);

        _realtimeNotifierMock.Verify(
            n => n.NotifyMessagesReadAsync(SenderId, ConversationId),
            Times.Once);
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_ConversationNotFound_ReturnsFailure()
    {
        SetupConversationByIdLookup(null);

        var result = await _sut.MarkConversationAsReadAsync(ConversationId, RecipientId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_NotParticipant_ReturnsFailure()
    {
        var conversation = CreateConversation(); // participants are SenderId and RecipientId
        SetupConversationByIdLookup(conversation);

        var nonParticipant = Guid.NewGuid();
        var result = await _sut.MarkConversationAsReadAsync(ConversationId, nonParticipant);

        Assert.False(result.IsSuccessful);
        Assert.Contains("not a participant", result.Message);
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.ConversationQueries.GetByAsync(It.IsAny<Expression<Func<Conversation, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.MarkConversationAsReadAsync(ConversationId, RecipientId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("DB error", result.Message);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void SetupSenderAndRecipient()
    {
        var sender = CreateCustomer(SenderId, "John", "Doe");
        var recipient = CreateCustomer(RecipientId, "Jane", "Smith");
        _unitOfWorkMock.SetupSequence(u => u.CustomerQueries.GetByAsync(
                It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(sender)
            .ReturnsAsync(recipient);
    }

    private void SetupCustomerLookup(Customer? customer)
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customer);
    }

    private void SetupConversationLookup(Conversation? conversation)
    {
        _unitOfWorkMock
            .Setup(u => u.ConversationQueries.GetByAsync(It.IsAny<Expression<Func<Conversation, bool>>>()))
            .ReturnsAsync(conversation);
    }

    private void SetupConversationByIdLookup(Conversation? conversation)
    {
        _unitOfWorkMock
            .Setup(u => u.ConversationQueries.GetByAsync(It.IsAny<Expression<Func<Conversation, bool>>>()))
            .ReturnsAsync(conversation);
    }

    private List<ChatMessage> SetupUnreadMessages(Guid conversationId, Guid senderId, int count)
    {
        var messages = Enumerable.Range(0, count)
            .Select(_ => new ChatMessage(conversationId, senderId, "Unread message") { IsRead = false })
            .ToList();

        _unitOfWorkMock
            .Setup(u => u.ChatMessageQueries.GetAllAsync(It.IsAny<Expression<Func<ChatMessage, bool>>>()))
            .ReturnsAsync(messages);

        return messages;
    }

    private void SetupSaveAsync()
    {
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
    }
}
