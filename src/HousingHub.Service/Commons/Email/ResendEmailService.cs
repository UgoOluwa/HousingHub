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
        string verifyLink = $"{baseUrl}/verify-email?email={Uri.EscapeDataString(toEmail)}&token={verificationToken}";

        string html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"><title>Verify your HousingHub email</title></head>
            <body style="margin:0;padding:0;background-color:#f0f4f9;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f0f4f9;">
                <tr><td align="center" style="padding:48px 16px;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:600px;">
                    <tr>
                      <td style="background-color:#07358B;border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                        <span style="font-size:32px;font-weight:800;color:#FFCC00;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Housing</span><span style="font-size:32px;font-weight:800;color:#ffffff;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Hub</span>
                      </td>
                    </tr>
                    <tr>
                      <td style="background-color:#ffffff;padding:44px 48px;border-radius:0 0 12px 12px;">
                        <h1 style="margin:0 0 12px 0;font-size:24px;font-weight:700;color:#07358B;font-family:Arial,Helvetica,sans-serif;">Welcome to HousingHub, {firstName}!</h1>
                        <p style="margin:0 0 8px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          We are thrilled to have you on board. To complete your registration and start exploring properties, please verify your email address.
                        </p>
                        <p style="margin:0 0 28px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          Click the button below to confirm your account:
                        </p>
                        <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:32px;">
                          <tr>
                            <td style="border-radius:8px;background-color:#07358B;">
                              <a href="{verifyLink}" style="display:inline-block;padding:14px 36px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:8px;font-family:Arial,Helvetica,sans-serif;letter-spacing:0.4px;">Verify My Email</a>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:0 0 8px 0;font-size:13px;color:#888888;font-family:Arial,Helvetica,sans-serif;">Or copy and paste this link into your browser:</p>
                        <p style="margin:0 0 28px 0;font-size:12px;color:#07358B;word-break:break-all;font-family:Arial,Helvetica,sans-serif;">{verifyLink}</p>
                        <p style="margin:0;font-size:14px;color:#888888;line-height:1.6;font-family:Arial,Helvetica,sans-serif;">
                          This link expires in <strong>24 hours</strong>. If you did not create a HousingHub account, you can safely ignore this email.
                        </p>
                        <hr style="border:none;border-top:1px solid #eeeeee;margin:32px 0 0 0;">
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:24px 40px;text-align:center;">
                        <p style="margin:0 0 6px 0;font-size:12px;color:#999999;font-family:Arial,Helvetica,sans-serif;">&copy; 2025 HousingHub. All rights reserved.</p>
                        <p style="margin:0;font-size:12px;color:#bbbbbb;font-family:Arial,Helvetica,sans-serif;">This is an automated message, please do not reply.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        string text = $"Welcome to HousingHub, {firstName}! Verify your email by visiting: {verifyLink}. This link expires in 24 hours.";

        return await SendAsync(toEmail, "Verify your HousingHub email", text, html);
    }

    public async Task<bool> SendPasswordResetAsync(string toEmail, string firstName, string resetToken)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";
        string resetLink = $"{baseUrl}/create-new-password?email={Uri.EscapeDataString(toEmail)}&token={resetToken}";

        string html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"><title>Reset your HousingHub password</title></head>
            <body style="margin:0;padding:0;background-color:#f0f4f9;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f0f4f9;">
                <tr><td align="center" style="padding:48px 16px;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:600px;">
                    <tr>
                      <td style="background-color:#07358B;border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                        <span style="font-size:32px;font-weight:800;color:#FFCC00;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Housing</span><span style="font-size:32px;font-weight:800;color:#ffffff;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Hub</span>
                      </td>
                    </tr>
                    <tr>
                      <td style="background-color:#ffffff;padding:44px 48px;border-radius:0 0 12px 12px;">
                        <h1 style="margin:0 0 12px 0;font-size:24px;font-weight:700;color:#07358B;font-family:Arial,Helvetica,sans-serif;">Password Reset Request</h1>
                        <p style="margin:0 0 8px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">Hi {firstName},</p>
                        <p style="margin:0 0 28px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          We received a request to reset the password for your HousingHub account. Click the button below to choose a new password. This link is valid for <strong>1 hour</strong>.
                        </p>
                        <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:32px;">
                          <tr>
                            <td style="border-radius:8px;background-color:#07358B;">
                              <a href="{resetLink}" style="display:inline-block;padding:14px 36px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:8px;font-family:Arial,Helvetica,sans-serif;letter-spacing:0.4px;">Reset My Password</a>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:0 0 8px 0;font-size:13px;color:#888888;font-family:Arial,Helvetica,sans-serif;">Or copy and paste this link into your browser:</p>
                        <p style="margin:0 0 28px 0;font-size:12px;color:#07358B;word-break:break-all;font-family:Arial,Helvetica,sans-serif;">{resetLink}</p>
                        <p style="margin:0;font-size:14px;color:#888888;line-height:1.6;font-family:Arial,Helvetica,sans-serif;">
                          If you did not request a password reset, no action is needed. Your account remains secure.
                        </p>
                        <hr style="border:none;border-top:1px solid #eeeeee;margin:32px 0 0 0;">
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:24px 40px;text-align:center;">
                        <p style="margin:0 0 6px 0;font-size:12px;color:#999999;font-family:Arial,Helvetica,sans-serif;">&copy; 2025 HousingHub. All rights reserved.</p>
                        <p style="margin:0;font-size:12px;color:#bbbbbb;font-family:Arial,Helvetica,sans-serif;">This is an automated message, please do not reply.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        string text = $"Hi {firstName}, reset your HousingHub password by visiting: {resetLink}. This link expires in 1 hour.";

        return await SendAsync(toEmail, "Reset your HousingHub password", text, html);
    }

    public async Task<bool> SendPasswordChangedAsync(string toEmail, string firstName)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"><title>Your HousingHub password was changed</title></head>
            <body style="margin:0;padding:0;background-color:#f0f4f9;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f0f4f9;">
                <tr><td align="center" style="padding:48px 16px;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:600px;">
                    <tr>
                      <td style="background-color:#07358B;border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                        <span style="font-size:32px;font-weight:800;color:#FFCC00;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Housing</span><span style="font-size:32px;font-weight:800;color:#ffffff;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Hub</span>
                      </td>
                    </tr>
                    <tr>
                      <td style="background-color:#ffffff;padding:44px 48px;border-radius:0 0 12px 12px;">
                        <h1 style="margin:0 0 12px 0;font-size:24px;font-weight:700;color:#07358B;font-family:Arial,Helvetica,sans-serif;">Your Password Was Changed</h1>
                        <p style="margin:0 0 8px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">Hi {firstName},</p>
                        <p style="margin:0 0 24px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          This is a confirmation that the password for your HousingHub account was successfully changed. If this was you, no further action is needed.
                        </p>
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:28px;">
                          <tr>
                            <td style="background-color:#fff8e1;border-left:4px solid #FFCC00;border-radius:0 6px 6px 0;padding:16px 20px;">
                              <p style="margin:0 0 6px 0;font-size:15px;font-weight:700;color:#5a4000;font-family:Arial,Helvetica,sans-serif;">Did not make this change?</p>
                              <p style="margin:0;font-size:14px;color:#7a5c00;line-height:1.6;font-family:Arial,Helvetica,sans-serif;">
                                If you did not change your password, your account may be at risk. Please secure your account immediately.
                              </p>
                            </td>
                          </tr>
                        </table>
                        <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:28px;">
                          <tr>
                            <td style="border-radius:8px;background-color:#07358B;">
                              <a href="{baseUrl}/reset-password" style="display:inline-block;padding:14px 36px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:8px;font-family:Arial,Helvetica,sans-serif;letter-spacing:0.4px;">Secure My Account</a>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:0;font-size:14px;color:#888888;line-height:1.6;font-family:Arial,Helvetica,sans-serif;">
                          If you need further assistance, please contact our support team.
                        </p>
                        <hr style="border:none;border-top:1px solid #eeeeee;margin:32px 0 0 0;">
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:24px 40px;text-align:center;">
                        <p style="margin:0 0 6px 0;font-size:12px;color:#999999;font-family:Arial,Helvetica,sans-serif;">&copy; 2025 HousingHub. All rights reserved.</p>
                        <p style="margin:0;font-size:12px;color:#bbbbbb;font-family:Arial,Helvetica,sans-serif;">This is an automated message, please do not reply.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        string text = $"Hi {firstName}, your HousingHub password was just changed. If this wasn't you, reset your password immediately at {baseUrl}/reset-password.";

        return await SendAsync(toEmail, "Your HousingHub password was changed", text, html);
    }

    public async Task<bool> SendInspectionScheduledAsync(string ownerEmail, string ownerName, string customerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime, string? note)
    {
        string noteSection = string.IsNullOrWhiteSpace(note) ? "" : $"<p><strong>Note:</strong> {note}</p>";

        string html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"><title>New Inspection Request</title></head>
            <body style="margin:0;padding:0;background-color:#f0f4f9;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f0f4f9;">
                <tr><td align="center" style="padding:48px 16px;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:600px;">
                    <tr>
                      <td style="background-color:#07358B;border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                        <span style="font-size:32px;font-weight:800;color:#FFCC00;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Housing</span><span style="font-size:32px;font-weight:800;color:#ffffff;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Hub</span>
                      </td>
                    </tr>
                    <tr>
                      <td style="background-color:#ffffff;padding:44px 48px;border-radius:0 0 12px 12px;">
                        <h1 style="margin:0 0 12px 0;font-size:24px;font-weight:700;color:#07358B;font-family:Arial,Helvetica,sans-serif;">New Inspection Request</h1>
                        <p style="margin:0 0 8px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">Hi {ownerName},</p>
                        <p style="margin:0 0 28px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          Great news! <strong>{customerName}</strong> has requested an inspection for your property and is eager to take a look.
                        </p>
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:28px;">
                          <tr>
                            <td style="background-color:#f7f9fc;border-radius:8px;padding:24px 28px;">
                              <p style="margin:0 0 10px 0;font-size:13px;font-weight:700;color:#07358B;text-transform:uppercase;letter-spacing:0.8px;font-family:Arial,Helvetica,sans-serif;">Inspection Details</p>
                              <p style="margin:0 0 8px 0;font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;"><strong>Property:</strong> {propertyTitle}</p>
                              <p style="margin:0 0 8px 0;font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;"><strong>Requested by:</strong> {customerName}</p>
                              <p style="margin:0 0 8px 0;font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;"><strong>Date:</strong> {scheduledDate:yyyy-MM-dd}</p>
                              <p style="margin:0;font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;"><strong>Time:</strong> {scheduledTime:hh\:mm}</p>
                            </td>
                          </tr>
                        </table>
                        <div style="font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;line-height:1.7;">
                          {noteSection}
                        </div>
                        <p style="margin:0 0 28px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          Please log in to your HousingHub dashboard to accept or decline this inspection request.
                        </p>
                        <hr style="border:none;border-top:1px solid #eeeeee;margin:0 0 0 0;">
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:24px 40px;text-align:center;">
                        <p style="margin:0 0 6px 0;font-size:12px;color:#999999;font-family:Arial,Helvetica,sans-serif;">&copy; 2025 HousingHub. All rights reserved.</p>
                        <p style="margin:0;font-size:12px;color:#bbbbbb;font-family:Arial,Helvetica,sans-serif;">This is an automated message, please do not reply.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        string text = $"Hi {ownerName}, {customerName} has scheduled an inspection for {propertyTitle} on {scheduledDate:yyyy-MM-dd} at {scheduledTime:hh\\:mm}. Log in to respond.";

        return await SendAsync(ownerEmail, $"New Inspection Request for {propertyTitle}", text, html);
    }

    public async Task<bool> SendInspectionResponseAsync(string customerEmail, string customerName, string ownerName, string propertyTitle, string action, string? note, DateTime? rescheduledDate, TimeSpan? rescheduledTime)
    {
        string noteSection = string.IsNullOrWhiteSpace(note) ? "" : $"<p><strong>Note from owner:</strong> {note}</p>";
        string rescheduleSection = rescheduledDate.HasValue
            ? $"<p style=\"margin:0 0 8px 0;font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;\"><strong>New Date:</strong> {rescheduledDate.Value:yyyy-MM-dd}</p><p style=\"margin:0;font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;\"><strong>New Time:</strong> {rescheduledTime!.Value:hh\\:mm}</p>"
            : "";

        string html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"><title>Inspection {action}</title></head>
            <body style="margin:0;padding:0;background-color:#f0f4f9;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f0f4f9;">
                <tr><td align="center" style="padding:48px 16px;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:600px;">
                    <tr>
                      <td style="background-color:#07358B;border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                        <span style="font-size:32px;font-weight:800;color:#FFCC00;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Housing</span><span style="font-size:32px;font-weight:800;color:#ffffff;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Hub</span>
                      </td>
                    </tr>
                    <tr>
                      <td style="background-color:#ffffff;padding:44px 48px;border-radius:0 0 12px 12px;">
                        <h1 style="margin:0 0 12px 0;font-size:24px;font-weight:700;color:#07358B;font-family:Arial,Helvetica,sans-serif;">Inspection {action}</h1>
                        <p style="margin:0 0 8px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">Hi {customerName},</p>
                        <p style="margin:0 0 28px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          The property owner <strong>{ownerName}</strong> has <strong>{action.ToLower()}</strong> your inspection request for <strong>{propertyTitle}</strong>.
                        </p>
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:28px;">
                          <tr>
                            <td style="background-color:#f7f9fc;border-radius:8px;padding:24px 28px;">
                              <p style="margin:0 0 10px 0;font-size:13px;font-weight:700;color:#07358B;text-transform:uppercase;letter-spacing:0.8px;font-family:Arial,Helvetica,sans-serif;">Inspection Details</p>
                              <p style="margin:0 0 8px 0;font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;"><strong>Property:</strong> {propertyTitle}</p>
                              <p style="margin:0 0 8px 0;font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;"><strong>Status:</strong> {action}</p>
                              {rescheduleSection}
                            </td>
                          </tr>
                        </table>
                        <div style="font-size:15px;color:#333333;font-family:Arial,Helvetica,sans-serif;line-height:1.7;margin-bottom:28px;">
                          {noteSection}
                        </div>
                        <p style="margin:0 0 28px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          Please log in to your HousingHub dashboard for full details and to manage your inspections.
                        </p>
                        <hr style="border:none;border-top:1px solid #eeeeee;margin:0 0 0 0;">
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:24px 40px;text-align:center;">
                        <p style="margin:0 0 6px 0;font-size:12px;color:#999999;font-family:Arial,Helvetica,sans-serif;">&copy; 2025 HousingHub. All rights reserved.</p>
                        <p style="margin:0;font-size:12px;color:#bbbbbb;font-family:Arial,Helvetica,sans-serif;">This is an automated message, please do not reply.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        string text = $"Hi {customerName}, {ownerName} has {action.ToLower()} your inspection for {propertyTitle}. Log in for details.";

        return await SendAsync(customerEmail, $"Inspection {action} for {propertyTitle}", text, html);
    }

    public async Task<bool> SendNewMessageAsync(string recipientEmail, string recipientName, string senderName, string messagePreview)
    {
        string html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"><title>New message from {senderName}</title></head>
            <body style="margin:0;padding:0;background-color:#f0f4f9;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f0f4f9;">
                <tr><td align="center" style="padding:48px 16px;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:600px;">
                    <tr>
                      <td style="background-color:#07358B;border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                        <span style="font-size:32px;font-weight:800;color:#FFCC00;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Housing</span><span style="font-size:32px;font-weight:800;color:#ffffff;font-family:Arial,Helvetica,sans-serif;letter-spacing:1px;">Hub</span>
                      </td>
                    </tr>
                    <tr>
                      <td style="background-color:#ffffff;padding:44px 48px;border-radius:0 0 12px 12px;">
                        <h1 style="margin:0 0 12px 0;font-size:24px;font-weight:700;color:#07358B;font-family:Arial,Helvetica,sans-serif;">You have a new message</h1>
                        <p style="margin:0 0 8px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">Hi {recipientName},</p>
                        <p style="margin:0 0 28px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          <strong>{senderName}</strong> sent you a message on HousingHub:
                        </p>
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:28px;">
                          <tr>
                            <td style="background-color:#f7f9fc;border-radius:8px;padding:24px 28px;">
                              <p style="margin:0;font-size:15px;color:#333333;font-style:italic;font-family:Arial,Helvetica,sans-serif;">&ldquo;{messagePreview}&rdquo;</p>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:0 0 28px 0;font-size:16px;color:#444444;line-height:1.7;font-family:Arial,Helvetica,sans-serif;">
                          Log in to your HousingHub dashboard to reply.
                        </p>
                        <hr style="border:none;border-top:1px solid #eeeeee;margin:0 0 0 0;">
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:24px 40px;text-align:center;">
                        <p style="margin:0 0 6px 0;font-size:12px;color:#999999;font-family:Arial,Helvetica,sans-serif;">&copy; 2025 HousingHub. All rights reserved.</p>
                        <p style="margin:0;font-size:12px;color:#bbbbbb;font-family:Arial,Helvetica,sans-serif;">This is an automated message, please do not reply.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        string text = $"Hi {recipientName}, {senderName} sent you a message on HousingHub: \"{messagePreview}\". Log in to reply.";

        return await SendAsync(recipientEmail, $"New message from {senderName}", text, html);
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
