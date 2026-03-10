using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace HousingHub.Service.Commons.Email;

internal sealed class SendGridEmailService : IEmailService
{
    private readonly ISendGridClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(
        ISendGridClient client,
        IConfiguration configuration,
        ILogger<SendGridEmailService> logger)
    {
        _client = client;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken)
    {
        string subject = "Verify your HousingHub email";
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";
        string verifyLink = $"{baseUrl}/api/v1/Auth/verify-email?email={Uri.EscapeDataString(toEmail)}&token={verificationToken}";

        string htmlContent = $"""
            <h2>Welcome to HousingHub, {firstName}!</h2>
            <p>Please verify your email address by clicking the link below:</p>
            <p><a href="{verifyLink}" style="padding:10px 20px;background:#4F46E5;color:#fff;text-decoration:none;border-radius:5px;">Verify Email</a></p>
            <p>Or copy and paste this link into your browser:</p>
            <p>{verifyLink}</p>
            <p>This link expires in 24 hours.</p>
            <br/>
            <p>If you did not create an account, please ignore this email.</p>
            """;

        string plainTextContent = $"Welcome to HousingHub, {firstName}! Verify your email by visiting: {verifyLink}. This link expires in 24 hours.";

        return await SendAsync(toEmail, subject, plainTextContent, htmlContent);
    }

    public async Task<bool> SendPasswordResetAsync(string toEmail, string firstName, string resetToken)
    {
        string subject = "Reset your HousingHub password";
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";
        string resetLink = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(toEmail)}&token={resetToken}";

        string htmlContent = $"""
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

        string plainTextContent = $"Hi {firstName}, reset your HousingHub password by visiting: {resetLink}. This link expires in 1 hour.";

        return await SendAsync(toEmail, subject, plainTextContent, htmlContent);
    }

    public async Task<bool> SendInspectionScheduledAsync(string ownerEmail, string ownerName, string customerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime, string? note)
    {
        string subject = $"New Inspection Request for {propertyTitle}";
        string noteSection = string.IsNullOrWhiteSpace(note) ? "" : $"<p><strong>Note:</strong> {note}</p>";

        string htmlContent = $"""
            <h2>New Inspection Request</h2>
            <p>Hi {ownerName},</p>
            <p><strong>{customerName}</strong> has scheduled an inspection for your property <strong>{propertyTitle}</strong>.</p>
            <p><strong>Date:</strong> {scheduledDate:yyyy-MM-dd}</p>
            <p><strong>Time:</strong> {scheduledTime:hh\:mm}</p>
            {noteSection}
            <p>Please log in to your HousingHub dashboard to accept or decline this inspection request.</p>
            """;

        string plainTextContent = $"Hi {ownerName}, {customerName} has scheduled an inspection for {propertyTitle} on {scheduledDate:yyyy-MM-dd} at {scheduledTime:hh\\:mm}. Log in to respond.";

        return await SendAsync(ownerEmail, subject, plainTextContent, htmlContent);
    }

    public async Task<bool> SendInspectionResponseAsync(string customerEmail, string customerName, string ownerName, string propertyTitle, string action, string? note, DateTime? rescheduledDate, TimeSpan? rescheduledTime)
    {
        string subject = $"Inspection {action} for {propertyTitle}";
        string noteSection = string.IsNullOrWhiteSpace(note) ? "" : $"<p><strong>Note:</strong> {note}</p>";
        string rescheduleSection = rescheduledDate.HasValue
            ? $"<p><strong>New Date:</strong> {rescheduledDate.Value:yyyy-MM-dd}</p><p><strong>New Time:</strong> {rescheduledTime!.Value:hh\\:mm}</p>"
            : "";

        string htmlContent = $"""
            <h2>Inspection {action}</h2>
            <p>Hi {customerName},</p>
            <p>The property owner <strong>{ownerName}</strong> has <strong>{action.ToLower()}</strong> your inspection request for <strong>{propertyTitle}</strong>.</p>
            {rescheduleSection}
            {noteSection}
            <p>Please log in to your HousingHub dashboard for more details.</p>
            """;

        string plainTextContent = $"Hi {customerName}, {ownerName} has {action.ToLower()} your inspection for {propertyTitle}. Log in for details.";

        return await SendAsync(customerEmail, subject, plainTextContent, htmlContent);
    }

    private async Task<bool> SendAsync(string toEmail, string subject, string plainTextContent, string htmlContent)
    {
        try
        {
            string fromEmail = _configuration["Email:SenderEmail"]!;
            string fromName = _configuration["Email:SenderName"] ?? "HousingHub";

            var from = new EmailAddress(fromEmail, fromName);
            var to = new EmailAddress(toEmail);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            var response = await _client.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
                return true;
            }

            string body = await response.Body.ReadAsStringAsync();
            _logger.LogWarning("SendGrid returned {StatusCode} for {Email}: {Body}",
                response.StatusCode, toEmail, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return false;
        }
    }
}
