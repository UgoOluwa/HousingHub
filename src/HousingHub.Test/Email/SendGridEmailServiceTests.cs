using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SendGrid;
using SendGrid.Helpers.Mail;
using HousingHub.Service.Commons.Email;

namespace HousingHub.Test.Email;

public class SendGridEmailServiceTests
{
    private readonly Mock<ISendGridClient> _sendGridClientMock;
    private readonly ILogger<SendGridEmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SendGridEmailService _sut;

    public SendGridEmailServiceTests()
    {
        _sendGridClientMock = new Mock<ISendGridClient>();
        _logger = NullLogger<SendGridEmailService>.Instance;

        var configData = new Dictionary<string, string?>
        {
            { "Email:SenderEmail", "noreply@housinghub.com" },
            { "Email:SenderName", "HousingHub" },
            { "Email:BaseUrl", "https://housinghub.com" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _sut = new SendGridEmailService(_sendGridClientMock.Object, _configuration, _logger);
    }

    // ??? SendEmailVerificationAsync ???????????????????????????????????

    [Fact]
    public async Task SendEmailVerificationAsync_WhenSendGridReturns202_ReturnsTrue()
    {
        // Arrange
        var response = new Response(HttpStatusCode.Accepted, null, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        bool result = await _sut.SendEmailVerificationAsync("user@test.com", "John", "abc123token");

        // Assert
        Assert.True(result);
        _sendGridClientMock.Verify(
            c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_WhenSendGridReturns400_ReturnsFalse()
    {
        // Arrange
        var body = new StringContent("{\"errors\":[{\"message\":\"Bad request\"}]}");
        var response = new Response(HttpStatusCode.BadRequest, body, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        bool result = await _sut.SendEmailVerificationAsync("user@test.com", "John", "abc123token");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_WhenSendGridThrows_ReturnsFalse()
    {
        // Arrange
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        bool result = await _sut.SendEmailVerificationAsync("user@test.com", "John", "abc123token");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_BuildsCorrectMessage()
    {
        // Arrange
        SendGridMessage? capturedMessage = null;
        var response = new Response(HttpStatusCode.Accepted, null, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        await _sut.SendEmailVerificationAsync("user@test.com", "John", "verify-token-123");

        // Assert
        Assert.NotNull(capturedMessage);
        string json = capturedMessage.Serialize();
        Assert.Contains("noreply@housinghub.com", json);
        Assert.Contains("HousingHub", json);
        Assert.Contains("Verify your HousingHub email", json);
        Assert.Contains("verify-token-123", json);
        Assert.Contains("John", json);
        Assert.Contains("https://housinghub.com/api/v1/Auth/verify-email", json);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_EscapesEmailInVerifyLink()
    {
        // Arrange
        SendGridMessage? capturedMessage = null;
        var response = new Response(HttpStatusCode.Accepted, null, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        await _sut.SendEmailVerificationAsync("user+test@test.com", "Jane", "token");

        // Assert
        Assert.NotNull(capturedMessage);
        string json = capturedMessage.Serialize();
        Assert.Contains("user%2Btest%40test.com", json);
    }

    // ??? SendPasswordResetAsync ???????????????????????????????????????

    [Fact]
    public async Task SendPasswordResetAsync_WhenSendGridReturns202_ReturnsTrue()
    {
        // Arrange
        var response = new Response(HttpStatusCode.Accepted, null, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        bool result = await _sut.SendPasswordResetAsync("user@test.com", "John", "reset-token-456");

        // Assert
        Assert.True(result);
        _sendGridClientMock.Verify(
            c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetAsync_WhenSendGridReturns401_ReturnsFalse()
    {
        // Arrange
        var body = new StringContent("{\"errors\":[{\"message\":\"Unauthorized\"}]}");
        var response = new Response(HttpStatusCode.Unauthorized, body, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        bool result = await _sut.SendPasswordResetAsync("user@test.com", "John", "reset-token-456");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendPasswordResetAsync_WhenSendGridThrows_ReturnsFalse()
    {
        // Arrange
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Timeout"));

        // Act
        bool result = await _sut.SendPasswordResetAsync("user@test.com", "John", "reset-token-456");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendPasswordResetAsync_BuildsCorrectMessage()
    {
        // Arrange
        SendGridMessage? capturedMessage = null;
        var response = new Response(HttpStatusCode.Accepted, null, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        await _sut.SendPasswordResetAsync("user@test.com", "John", "reset-token-789");

        // Assert
        Assert.NotNull(capturedMessage);
        string json = capturedMessage.Serialize();
        Assert.Contains("noreply@housinghub.com", json);
        Assert.Contains("HousingHub", json);
        Assert.Contains("Reset your HousingHub password", json);
        Assert.Contains("reset-token-789", json);
        Assert.Contains("John", json);
        Assert.Contains("https://housinghub.com/reset-password", json);
    }

    // ??? Configuration edge cases ?????????????????????????????????????

    [Fact]
    public async Task SendEmailVerificationAsync_UsesDefaultBaseUrl_WhenConfigMissing()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Email:SenderEmail", "test@test.com" },
            { "Email:SenderName", "Test" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var sut = new SendGridEmailService(_sendGridClientMock.Object, config, _logger);

        SendGridMessage? capturedMessage = null;
        var response = new Response(HttpStatusCode.Accepted, null, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        await sut.SendEmailVerificationAsync("user@test.com", "Jane", "token");

        // Assert
        Assert.NotNull(capturedMessage);
        string json = capturedMessage.Serialize();
        Assert.Contains("https://localhost/api/v1/Auth/verify-email", json);
    }

    [Fact]
    public async Task SendPasswordResetAsync_UsesDefaultBaseUrl_WhenConfigMissing()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Email:SenderEmail", "test@test.com" },
            { "Email:SenderName", "Test" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var sut = new SendGridEmailService(_sendGridClientMock.Object, config, _logger);

        SendGridMessage? capturedMessage = null;
        var response = new Response(HttpStatusCode.Accepted, null, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        await sut.SendPasswordResetAsync("user@test.com", "Jane", "token");

        // Assert
        Assert.NotNull(capturedMessage);
        string json = capturedMessage.Serialize();
        Assert.Contains("https://localhost/reset-password", json);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_UsesDefaultSenderName_WhenConfigMissing()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Email:SenderEmail", "test@test.com" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var sut = new SendGridEmailService(_sendGridClientMock.Object, config, _logger);

        SendGridMessage? capturedMessage = null;
        var response = new Response(HttpStatusCode.Accepted, null, null);
        _sendGridClientMock
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        await sut.SendEmailVerificationAsync("user@test.com", "Jane", "token");

        // Assert
        Assert.NotNull(capturedMessage);
        string json = capturedMessage.Serialize();
        Assert.Contains("HousingHub", json);
    }
}
