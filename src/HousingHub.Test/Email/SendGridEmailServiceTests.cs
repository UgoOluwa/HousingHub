using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using HousingHub.Service.Commons.Email;

namespace HousingHub.Test.Email;

public class ResendEmailServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly IConfiguration _configuration;

    public ResendEmailServiceTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Email:ResendApiKey", "re_test_key" },
                { "Email:SenderEmail", "noreply@housinghub.com" },
                { "Email:SenderName", "HousingHub" },
                { "Email:BaseUrl", "https://housinghub.com" }
            })
            .Build();
    }

    private ResendEmailService BuildSut(HttpStatusCode statusCode, string? responseBody = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (responseBody != null)
            response.Content = new StringContent(responseBody);

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(_handlerMock.Object);
        return new ResendEmailService(httpClient, _configuration, NullLogger<ResendEmailService>.Instance);
    }

    // ─── SendEmailVerificationAsync ───────────────────────────────────

    [Fact]
    public async Task SendEmailVerificationAsync_WhenResendReturns200_ReturnsTrue()
    {
        var sut = BuildSut(HttpStatusCode.OK);
        bool result = await sut.SendEmailVerificationAsync("user@test.com", "John", "abc123token");
        Assert.True(result);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_WhenResendReturns400_ReturnsFalse()
    {
        var sut = BuildSut(HttpStatusCode.BadRequest, "{\"name\":\"validation_error\"}");
        bool result = await sut.SendEmailVerificationAsync("user@test.com", "John", "abc123token");
        Assert.False(result);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_WhenResendThrows_ReturnsFalse()
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(_handlerMock.Object);
        var sut = new ResendEmailService(httpClient, _configuration, NullLogger<ResendEmailService>.Instance);

        bool result = await sut.SendEmailVerificationAsync("user@test.com", "John", "abc123token");
        Assert.False(result);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_SendsCorrectPayload()
    {
        HttpRequestMessage? captured = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(_handlerMock.Object);
        var sut = new ResendEmailService(httpClient, _configuration, NullLogger<ResendEmailService>.Instance);

        await sut.SendEmailVerificationAsync("user@test.com", "John", "verify-token-123");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("https://api.resend.com/emails", captured.RequestUri!.ToString());

        string body = await captured.Content!.ReadAsStringAsync();
        Assert.Contains("noreply@housinghub.com", body);
        Assert.Contains("Verify your HousingHub email", body);
        Assert.Contains("verify-token-123", body);
        Assert.Contains("John", body);
        Assert.Contains("https://housinghub.com/verify-email", body);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_EscapesEmailInVerifyLink()
    {
        HttpRequestMessage? captured = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(_handlerMock.Object);
        var sut = new ResendEmailService(httpClient, _configuration, NullLogger<ResendEmailService>.Instance);

        await sut.SendEmailVerificationAsync("user+test@test.com", "Jane", "token");

        string body = await captured!.Content!.ReadAsStringAsync();
        Assert.Contains("user%2Btest%40test.com", body);
    }

    // ─── SendPasswordResetAsync ───────────────────────────────────────

    [Fact]
    public async Task SendPasswordResetAsync_WhenResendReturns200_ReturnsTrue()
    {
        var sut = BuildSut(HttpStatusCode.OK);
        bool result = await sut.SendPasswordResetAsync("user@test.com", "John", "reset-token-456");
        Assert.True(result);
    }

    [Fact]
    public async Task SendPasswordResetAsync_WhenResendReturns401_ReturnsFalse()
    {
        var sut = BuildSut(HttpStatusCode.Unauthorized, "{\"name\":\"missing_api_key\"}");
        bool result = await sut.SendPasswordResetAsync("user@test.com", "John", "reset-token-456");
        Assert.False(result);
    }

    [Fact]
    public async Task SendPasswordResetAsync_SendsCorrectPayload()
    {
        HttpRequestMessage? captured = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(_handlerMock.Object);
        var sut = new ResendEmailService(httpClient, _configuration, NullLogger<ResendEmailService>.Instance);

        await sut.SendPasswordResetAsync("user@test.com", "John", "reset-token-789");

        string body = await captured!.Content!.ReadAsStringAsync();
        Assert.Contains("Reset your HousingHub password", body);
        Assert.Contains("reset-token-789", body);
        Assert.Contains("https://housinghub.com/create-new-password", body);
    }

    // ─── Configuration edge cases ─────────────────────────────────────

    [Fact]
    public async Task SendEmailVerificationAsync_UsesDefaultBaseUrl_WhenConfigMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Email:ResendApiKey", "re_test" },
                { "Email:SenderEmail", "test@test.com" }
            })
            .Build();

        HttpRequestMessage? captured = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var sut = new ResendEmailService(new HttpClient(_handlerMock.Object), config, NullLogger<ResendEmailService>.Instance);

        await sut.SendEmailVerificationAsync("user@test.com", "Jane", "token");

        string body = await captured!.Content!.ReadAsStringAsync();
        Assert.Contains("https://localhost/verify-email", body);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_UsesDefaultSenderName_WhenConfigMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Email:ResendApiKey", "re_test" },
                { "Email:SenderEmail", "test@test.com" }
            })
            .Build();

        HttpRequestMessage? captured = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var sut = new ResendEmailService(new HttpClient(_handlerMock.Object), config, NullLogger<ResendEmailService>.Instance);

        await sut.SendEmailVerificationAsync("user@test.com", "Jane", "token");

        string body = await captured!.Content!.ReadAsStringAsync();
        Assert.Contains("HousingHub", body);
    }
}
