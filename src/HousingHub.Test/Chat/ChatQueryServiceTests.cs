using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.ChatService;
using HousingHub.Service.Dtos.Chat;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Chat;

public class ChatQueryServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly ChatQueryService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid ConversationId = Guid.NewGuid();

    public ChatQueryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk>();
        var logger = NullLogger<ChatQueryService>.Instance;
        _sut = new ChatQueryService(_unitOfWorkMock.Object, logger);
    }

    private static Customer CreateCustomer(Guid id, string firstName = "Test", string lastName = "User") =>
        new(firstName, lastName, $"{firstName.ToLower()}@test.com", "08012345678", CustomerType.Customer, "hash")
        {
            Id = id
        };

    private static Conversation CreateConversation(
        Guid? id = null, Guid? participantOneId = null, Guid? participantTwoId = null) => new()
    {
        Id = id ?? ConversationId,
        ParticipantOneId = participantOneId ?? UserId,
        ParticipantTwoId = participantTwoId ?? OtherUserId,
        LastMessage = "Hello",
        LastMessageAt = DateTime.UtcNow
    };

    private static ChatMessage CreateMessage(
        Guid conversationId, Guid senderId, string content = "Test message", bool isRead = false) =>
        new(conversationId, senderId, content)
        {
            IsRead = isRead,
            DateCreated = DateTime.UtcNow
        };

    // ── GetConversationsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetConversationsAsync_ReturnsConversations()
    {
        var conversation = CreateConversation();
        SetupConversationsList(new List<Conversation> { conversation });
        SetupParticipants(new List<Customer> { CreateCustomer(OtherUserId, "Jane", "Smith") });
        SetupAllUnreadMessages(new List<ChatMessage>());

        var result = await _sut.GetConversationsAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsEmptyList_WhenNoConversations()
    {
        SetupConversationsList(new List<Conversation>());
        SetupParticipants(new List<Customer>());
        SetupAllUnreadMessages(new List<ChatMessage>());

        var result = await _sut.GetConversationsAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetConversationsAsync_IncludesParticipantName()
    {
        var conversation = CreateConversation();
        SetupConversationsList(new List<Conversation> { conversation });
        SetupParticipants(new List<Customer> { CreateCustomer(OtherUserId, "Jane", "Smith") });
        SetupAllUnreadMessages(new List<ChatMessage>());

        var result = await _sut.GetConversationsAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Jane Smith", result.Data![0].ParticipantName);
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsUnknownUser_WhenParticipantMissing()
    {
        var conversation = CreateConversation();
        SetupConversationsList(new List<Conversation> { conversation });
        SetupParticipants(new List<Customer>()); // participant not found
        SetupAllUnreadMessages(new List<ChatMessage>());

        var result = await _sut.GetConversationsAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Unknown User", result.Data![0].ParticipantName);
    }

    [Fact]
    public async Task GetConversationsAsync_IncludesUnreadCount()
    {
        var conversation = CreateConversation();
        SetupConversationsList(new List<Conversation> { conversation });
        SetupParticipants(new List<Customer> { CreateCustomer(OtherUserId, "Jane", "Smith") });

        // 3 unread messages from OtherUserId in this conversation
        var unreadMessages = Enumerable.Range(0, 3)
            .Select(_ => CreateMessage(ConversationId, OtherUserId))
            .ToList();
        SetupAllUnreadMessages(unreadMessages);

        var result = await _sut.GetConversationsAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(3, result.Data![0].UnreadCount);
    }

    [Fact]
    public async Task GetConversationsAsync_OrdersByMostRecent()
    {
        var oldConversation = CreateConversation(Guid.NewGuid());
        oldConversation.LastMessageAt = DateTime.UtcNow.AddHours(-2);

        var newConversation = CreateConversation(Guid.NewGuid());
        newConversation.LastMessageAt = DateTime.UtcNow;

        SetupConversationsList(new List<Conversation> { oldConversation, newConversation });
        SetupParticipants(new List<Customer> { CreateCustomer(OtherUserId, "Jane", "Smith") });
        SetupAllUnreadMessages(new List<ChatMessage>());

        var result = await _sut.GetConversationsAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Count);
        // The newer conversation should be first
        Assert.Equal(newConversation.Id, result.Data[0].Id);
        Assert.Equal(oldConversation.Id, result.Data[1].Id);
    }

    [Fact]
    public async Task GetConversationsAsync_IncludesLastMessage()
    {
        var conversation = CreateConversation();
        conversation.LastMessage = "Last msg preview";
        SetupConversationsList(new List<Conversation> { conversation });
        SetupParticipants(new List<Customer> { CreateCustomer(OtherUserId, "Jane", "Smith") });
        SetupAllUnreadMessages(new List<ChatMessage>());

        var result = await _sut.GetConversationsAsync(UserId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Last msg preview", result.Data![0].LastMessage);
    }

    [Fact]
    public async Task GetConversationsAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.ConversationQueries.GetAllAsync(It.IsAny<Expression<Func<Conversation, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.GetConversationsAsync(UserId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("DB error", result.Message);
    }

    // ── GetMessagesAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetMessagesAsync_ReturnsMessages()
    {
        var conversation = CreateConversation();
        SetupConversationByIdLookup(conversation);

        var messages = new List<ChatMessage>
        {
            CreateMessage(ConversationId, UserId, "Hello"),
            CreateMessage(ConversationId, OtherUserId, "Hi back")
        };
        SetupConversationMessages(messages);
        SetupMessageSenders(new List<Customer>
        {
            CreateCustomer(UserId, "John", "Doe"),
            CreateCustomer(OtherUserId, "Jane", "Smith")
        });

        var result = await _sut.GetMessagesAsync(ConversationId, UserId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
    }

    [Fact]
    public async Task GetMessagesAsync_ConversationNotFound_ReturnsFailure()
    {
        SetupConversationByIdLookup(null);

        var result = await _sut.GetMessagesAsync(ConversationId, UserId, 1, 10);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task GetMessagesAsync_NotParticipant_ReturnsFailure()
    {
        var conversation = CreateConversation(); // participants are UserId and OtherUserId
        SetupConversationByIdLookup(conversation);

        var nonParticipant = Guid.NewGuid();
        var result = await _sut.GetMessagesAsync(ConversationId, nonParticipant, 1, 10);

        Assert.False(result.IsSuccessful);
        Assert.Contains("not a participant", result.Message);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsPaginatedResult()
    {
        var conversation = CreateConversation();
        SetupConversationByIdLookup(conversation);

        // Create 5 messages
        var messages = Enumerable.Range(0, 5)
            .Select(i =>
            {
                var msg = CreateMessage(ConversationId, UserId, $"Message {i}");
                msg.DateCreated = DateTime.UtcNow.AddMinutes(-i);
                return msg;
            })
            .ToList();
        SetupConversationMessages(messages);
        SetupMessageSenders(new List<Customer> { CreateCustomer(UserId, "John", "Doe") });

        var result = await _sut.GetMessagesAsync(ConversationId, UserId, 1, 3);

        Assert.True(result.IsSuccessful);
        Assert.Equal(5, result.Data!.TotalCount);
        Assert.Equal(3, result.Data.Items.Count);
        Assert.Equal(1, result.Data.PageNumber);
        Assert.Equal(3, result.Data.PageSize);
    }

    [Fact]
    public async Task GetMessagesAsync_IncludesSenderNames()
    {
        var conversation = CreateConversation();
        SetupConversationByIdLookup(conversation);

        var messages = new List<ChatMessage> { CreateMessage(ConversationId, UserId, "Hi") };
        SetupConversationMessages(messages);
        SetupMessageSenders(new List<Customer> { CreateCustomer(UserId, "John", "Doe") });

        var result = await _sut.GetMessagesAsync(ConversationId, UserId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.Equal("John Doe", result.Data!.Items[0].SenderName);
    }

    [Fact]
    public async Task GetMessagesAsync_EmptyConversation_ReturnsEmptyItems()
    {
        var conversation = CreateConversation();
        SetupConversationByIdLookup(conversation);
        SetupConversationMessages(new List<ChatMessage>());
        SetupMessageSenders(new List<Customer>());

        var result = await _sut.GetMessagesAsync(ConversationId, UserId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.ConversationQueries.GetByAsync(It.IsAny<Expression<Func<Conversation, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.GetMessagesAsync(ConversationId, UserId, 1, 10);

        Assert.False(result.IsSuccessful);
        Assert.Contains("DB error", result.Message);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void SetupConversationsList(List<Conversation> conversations)
    {
        _unitOfWorkMock
            .Setup(u => u.ConversationQueries.GetAllAsync(It.IsAny<Expression<Func<Conversation, bool>>>()))
            .ReturnsAsync(conversations);
    }

    private void SetupParticipants(List<Customer> customers)
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customers);
    }

    private void SetupAllUnreadMessages(List<ChatMessage> messages)
    {
        _unitOfWorkMock
            .Setup(u => u.ChatMessageQueries.GetAllAsync(It.IsAny<Expression<Func<ChatMessage, bool>>>()))
            .ReturnsAsync(messages);
    }

    private void SetupConversationByIdLookup(Conversation? conversation)
    {
        _unitOfWorkMock
            .Setup(u => u.ConversationQueries.GetByAsync(It.IsAny<Expression<Func<Conversation, bool>>>()))
            .ReturnsAsync(conversation);
    }

    private void SetupConversationMessages(List<ChatMessage> messages)
    {
        _unitOfWorkMock
            .Setup(u => u.ChatMessageQueries.GetAllAsync(It.IsAny<Expression<Func<ChatMessage, bool>>>()))
            .ReturnsAsync(messages);
    }

    private void SetupMessageSenders(List<Customer> customers)
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customers);
    }
}
