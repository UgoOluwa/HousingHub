using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.Commons.Email;

internal sealed class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient httpClient, IConfiguration configuration, ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";
        string verifyLink = $"{baseUrl}/verify?email={Uri.EscapeDataString(toEmail)}&token={verificationToken}";

        string html = $"""
            <h2>Welcome to HousingHub, {firstName}!</h2>
            <p>Please verify your email address by clicking the link below:</p>
            <p><a href="{verifyLink}" style="padding:10px 20px;background:#4F46E5;color:#fff;text-decoration:none;border-radius:5px;">Verify Email</a></p>
            <p>Or copy and paste this link into your browser:</p>
            <p>{verifyLink}</p>
            <p>This link expires in 24 hours.</p>
            <br/>
            <p>If you did not create an account, please ignore this email.</p>
            """;

        string text = $"Welcome to HousingHub, {firstName}! Verify your email by visiting: {verifyLink}. This link expires in 24 hours.";

        return await SendAsync(toEmail, "Verify your HousingHub email", text, html);
    }

    public async Task<bool> SendPasswordResetAsync(string toEmail, string firstName, string resetToken)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";
        string resetLink = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(toEmail)}&token={resetToken}";

        string html = $"""
            <h2>Password Reset Request</h2>
            <p>Hi {firstName},</p>
            <p>We received a request to reset your password. Click the link below to set a new password:</p>
            <p><a href="{resetLink}" style="padding:10px 20px;background:#4F46E5;color:#fff;text-decoration:none;border-radius:5px;">Reset Password</a></p>
            <p>Or copy and paste this link into your browser:</p>
            <p>{resetLink}</p>
            <p>This link expires in 1 hour.</p>
            <br/>
            <p>If you did not request a password reset, please ignore this email.</p>
            """;

        string text = $"Hi {firstName}, reset your HousingHub password by visiting: {resetLink}. This link expires in 1 hour.";

        return await SendAsync(toEmail, "Reset your HousingHub password", text, html);
    }

    public async Task<bool> SendInspectionScheduledAsync(string ownerEmail, string ownerName, string customerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime, string? note)
    {
        string noteSection = string.IsNullOrWhiteSpace(note) ? "" : $"<p><strong>Note:</strong> {note}</p>";

        string html = $"""
            <h2>New Inspection Request</h2>
            <p>Hi {ownerName},</p>
            <p><strong>{customerName}</strong> has scheduled an inspection for your property <strong>{propertyTitle}</strong>.</p>
            <p><strong>Date:</strong> {scheduledDate:yyyy-MM-dd}</p>
            <p><strong>Time:</strong> {scheduledTime:hh\:mm}</p>
            {noteSection}
            <p>Please log in to your HousingHub dashboard to accept or decline this inspection request.</p>
            """;

        string text = $"Hi {ownerName}, {customerName} has scheduled an inspection for {propertyTitle} on {scheduledDate:yyyy-MM-dd} at {scheduledTime:hh\\:mm}. Log in to respond.";

        return await SendAsync(ownerEmail, $"New Inspection Request for {propertyTitle}", text, html);
    }

    public async Task<bool> SendInspectionResponseAsync(string customerEmail, string customerName, string ownerName, string propertyTitle, string action, string? note, DateTime? rescheduledDate, TimeSpan? rescheduledTime)
    {
        string noteSection = string.IsNullOrWhiteSpace(note) ? "" : $"<p><strong>Note:</strong> {note}</p>";
        string rescheduleSection = rescheduledDate.HasValue
            ? $"<p><strong>New Date:</strong> {rescheduledDate.Value:yyyy-MM-dd}</p><p><strong>New Time:</strong> {rescheduledTime!.Value:hh\\:mm}</p>"
            : "";

        string html = $"""
            <h2>Inspection {action}</h2>
            <p>Hi {customerName},</p>
            <p>The property owner <strong>{ownerName}</strong> has <strong>{action.ToLower()}</strong> your inspection request for <strong>{propertyTitle}</strong>.</p>
            {rescheduleSection}
            {noteSection}
            <p>Please log in to your HousingHub dashboard for more details.</p>
            """;

        string text = $"Hi {customerName}, {ownerName} has {action.ToLower()} your inspection for {propertyTitle}. Log in for details.";

        return await SendAsync(customerEmail, $"Inspection {action} for {propertyTitle}", text, html);
    }

    private async Task<bool> SendAsync(string toEmail, string subject, string text, string html)
    {
        try
        {
            string fromEmail = _configuration["Email:SenderEmail"]!;
            string fromName = _configuration["Email:SenderName"] ?? "HousingHub";
            string apiKey = _configuration["Email:ResendApiKey"]!;

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                from = $"{fromName} <{fromEmail}>",
                to = new[] { toEmail },
                subject,
                text,
                html
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.resend.com/emails", payload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
                return true;
            }

            string body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Resend returned {StatusCode} for {Email}: {Body}", response.StatusCode, toEmail, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return false;
        }
    }
}
