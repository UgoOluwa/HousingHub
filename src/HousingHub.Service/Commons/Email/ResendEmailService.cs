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

        string body = $"""
            {P($"Hi {firstName},")}
            {P("Welcome to Housing Hub. To finish setting up your account and start browsing verified listings, confirm your email address below.")}
            {Button("Verify My Email", verifyLink)}
            <p style="margin:20px 0 0 0;font-size:13px;color:#9AA3AE;line-height:1.6;font-family:{Font};">
              This link expires in <strong style="color:{Muted};">24 hours</strong>. If you did not create a Housing Hub account, you can safely ignore this email.
            </p>
            """;

        string html = WrapInLayout("Verify your Housing Hub email", body, Hero("&#9993;", "Confirm your email"));
        string text = $"Welcome to Housing Hub, {firstName}! Verify your email by visiting: {verifyLink}. This link expires in 24 hours.";

        return await SendAsync(toEmail, "Verify your Housing Hub email", text, html);
    }

    public async Task<bool> SendPasswordResetAsync(string toEmail, string firstName, string resetToken)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";
        string resetLink = $"{baseUrl}/create-new-password?email={Uri.EscapeDataString(toEmail)}&token={resetToken}";

        string body = $"""
            {P($"Hi {firstName},")}
            {P("We received a request to reset the password on your Housing Hub account. Choose a new one using the button below.")}
            {Button("Reset My Password", resetLink)}
            <p style="margin:20px 0 0 0;font-size:13px;color:#9AA3AE;line-height:1.6;font-family:{Font};">
              This link is valid for <strong style="color:{Muted};">1 hour</strong>. If you did not request a reset, no action is needed &mdash; your account remains secure.
            </p>
            """;

        string html = WrapInLayout("Reset your Housing Hub password", body, Hero("&#128273;", "Reset your password"));
        string text = $"Hi {firstName}, reset your Housing Hub password by visiting: {resetLink}. This link expires in 1 hour.";

        return await SendAsync(toEmail, "Reset your Housing Hub password", text, html);
    }

    public async Task<bool> SendRegistrationAttemptOnExistingAccountAsync(string toEmail, string firstName)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {firstName},")}
            {P("Someone just tried to create a Housing Hub account using this email address. You already have one, so we did not create a second.")}
            {P("If that was you — perhaps you forgot you had signed up — you can sign in below, or reset your password if you do not remember it.")}
            {Button("Sign In", $"{baseUrl}/login")}
            <p style="margin:20px 0 0 0;font-size:13px;color:#9AA3AE;line-height:1.6;font-family:{Font};">
              Forgotten your password? <a href="{baseUrl}/reset-password" style="color:{Navy};font-weight:700;">Reset it here</a>.
            </p>
            {Callout("Wasn't you?", "No action is needed — your account is unchanged and nobody gained access to it. If you keep receiving these, contact us at info@housinghub.ng.")}
            """;

        string html = WrapInLayout("You already have a Housing Hub account", body, Hero("&#128100;", "You already have an account"));
        string text = $"Hi {firstName}, someone tried to register a Housing Hub account with this email. You already have one — sign in at {baseUrl}/login, or reset your password at {baseUrl}/reset-password. If this wasn't you, no action is needed.";

        return await SendAsync(toEmail, "You already have a Housing Hub account", text, html);
    }

    public async Task<bool> SendPasswordChangedAsync(string toEmail, string firstName)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {firstName},")}
            {P("The password on your Housing Hub account was just changed successfully. If this was you, no further action is needed.")}
            {Callout("Didn't make this change?", "Your account may be at risk. Secure it immediately using the button below, then contact us at info@housinghub.ng.")}
            {Button("Secure My Account", $"{baseUrl}/reset-password")}
            """;

        string html = WrapInLayout("Your Housing Hub password was changed", body, Hero("&#128274;", "Password changed"));
        string text = $"Hi {firstName}, your Housing Hub password was just changed. If this wasn't you, reset your password immediately at {baseUrl}/reset-password.";

        return await SendAsync(toEmail, "Your Housing Hub password was changed", text, html);
    }

    public async Task<bool> SendInspectionScheduledAsync(string ownerEmail, string ownerName, string customerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime, string? note)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";
        string noteSection = string.IsNullOrWhiteSpace(note) ? "" : DetailRow("Note from customer", note);

        string body = $"""
            {P($"Hi {ownerName},")}
            {P($"<strong style=\"color:{Ink};\">{customerName}</strong> has requested an inspection for your property.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Requested by", customerName)}
            {DetailRow("Date &amp; time", $"{scheduledDate:dddd, d MMMM yyyy} &middot; {scheduledTime:hh\\:mm}")}
            {noteSection}
            {Button("Accept or Decline", $"{baseUrl}/inspections")}
            """;

        string html = WrapInLayout("New inspection request", body, Hero("&#127968;", "New inspection request"));
        string text = $"Hi {ownerName}, {customerName} has scheduled an inspection for {propertyTitle} on {scheduledDate:yyyy-MM-dd} at {scheduledTime:hh\\:mm}. Log in to respond.";

        return await SendAsync(ownerEmail, $"New Inspection Request for {propertyTitle}", text, html);
    }

    public async Task<bool> SendInspectionBookingConfirmationAsync(string customerEmail, string customerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime, string? note)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";
        string noteSection = string.IsNullOrWhiteSpace(note) ? "" : DetailRow("Your note", note);

        string body = $"""
            {P($"Hi {customerName},")}
            {P("Your inspection request has been submitted. The property owner will review it and respond shortly &mdash; we'll email you the moment they do.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Date &amp; time", $"{scheduledDate:dddd, d MMMM yyyy} &middot; {scheduledTime:hh\\:mm}")}
            {DetailRow("Status", "Awaiting owner response")}
            {noteSection}
            {Button("Track This Inspection", $"{baseUrl}/inspections")}
            """;

        string html = WrapInLayout("Inspection request submitted", body, Hero("&#128340;", "Request submitted"));
        string text = $"Hi {customerName}, your inspection request for {propertyTitle} on {scheduledDate:yyyy-MM-dd} at {scheduledTime:hh\\:mm} has been submitted. We'll notify you when the owner responds.";

        return await SendAsync(customerEmail, $"Inspection Request Submitted for {propertyTitle}", text, html);
    }

    public async Task<bool> SendInspectionResponseAsync(string customerEmail, string customerName, string ownerName, string propertyTitle, string action, string? note, DateTime? rescheduledDate, TimeSpan? rescheduledTime)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";
        string noteSection = string.IsNullOrWhiteSpace(note) ? "" : DetailRow("Note from owner", note);
        string rescheduleSection = rescheduledDate.HasValue
            ? DetailRow("New date &amp; time", $"{rescheduledDate.Value:dddd, d MMMM yyyy} &middot; {rescheduledTime!.Value:hh\\:mm}")
            : "";

        // Glyph tracks the outcome so the mail reads correctly at a glance.
        string glyph = action.ToLowerInvariant() switch
        {
            "confirmed" or "accepted" or "approved" => "&#10003;",
            "declined" or "rejected" or "cancelled" => "&#10005;",
            _ => "&#128197;",
        };

        string body = $"""
            {P($"Hi {customerName},")}
            {P($"<strong style=\"color:{Ink};\">{ownerName}</strong> has {action.ToLower()} your inspection request.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Status", action)}
            {rescheduleSection}
            {noteSection}
            {Button("View Inspection", $"{baseUrl}/inspections")}
            """;

        string html = WrapInLayout($"Inspection {action.ToLower()}", body, Hero(glyph, $"Inspection {action.ToLower()}"));
        string text = $"Hi {customerName}, {ownerName} has {action.ToLower()} your inspection for {propertyTitle}. Log in for details.";

        return await SendAsync(customerEmail, $"Inspection {action} for {propertyTitle}", text, html);
    }

    public async Task<bool> SendNewMessageAsync(string recipientEmail, string recipientName, string senderName, string messagePreview)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {recipientName},")}
            {P($"<strong style=\"color:{Ink};\">{senderName}</strong> sent you a message on Housing Hub.")}
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:4px 0 8px;">
              <tr>
                <td style="background-color:#F7F8FA;border-left:3px solid {Gold};padding:16px 18px;">
                  <p style="margin:0;font-size:15px;color:{Ink};font-style:italic;line-height:1.6;font-family:{Font};">&ldquo;{messagePreview}&rdquo;</p>
                </td>
              </tr>
            </table>
            {Button("Reply in Housing Hub", $"{baseUrl}/messages")}
            """;

        string html = WrapInLayout($"New message from {senderName}", body, Hero("&#128172;", "You have a new message"));
        string text = $"Hi {recipientName}, {senderName} sent you a message on Housing Hub: \"{messagePreview}\". Log in to reply.";

        return await SendAsync(recipientEmail, $"New message from {senderName}", text, html);
    }

    public async Task<bool> SendInspectionReminderAsync(string recipientEmail, string recipientName, string otherPartyName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {recipientName},")}
            {P($"Your inspection with <strong style=\"color:{Ink};\">{otherPartyName}</strong> is coming up in about 24 hours.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Date &amp; time", $"{scheduledDate:dddd, d MMMM yyyy} &middot; {scheduledTime:hh\\:mm}")}
            {DetailRow("Meeting with", otherPartyName)}
            {Callout("Before you go", "Please arrive on time and bring a valid means of identification. You can message the other party from your dashboard if anything changes.")}
            {Button("View Inspection", $"{baseUrl}/inspections")}
            """;

        string html = WrapInLayout("Inspection reminder", body, Hero("&#9200;", "Inspection in 24 hours"));
        string text = $"Hi {recipientName}, reminder: your inspection with {otherPartyName} for {propertyTitle} is in about 24 hours, on {scheduledDate:yyyy-MM-dd} at {scheduledTime:hh\\:mm}.";

        return await SendAsync(recipientEmail, $"Reminder: Inspection for {propertyTitle} in 24 hours", text, html);
    }

    public async Task<bool> SendAdminOtpAsync(string toEmail, string firstName, string otpCode)
    {
        string body = $"""
            {P($"Hi {firstName},")}
            {P("Use the code below to sign in to the Housing Hub admin dashboard.")}
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:8px 0 6px;">
              <tr>
                <td style="background-color:{HeroTint};border-radius:8px;padding:22px;text-align:center;">
                  <p style="margin:0;font-size:34px;font-weight:800;letter-spacing:10px;color:{Navy};font-family:{Font};">{otpCode}</p>
                </td>
              </tr>
            </table>
            <p style="margin:16px 0 0 0;font-size:13px;color:#9AA3AE;line-height:1.6;font-family:{Font};">
              This code expires in <strong style="color:{Muted};">10 minutes</strong>. If you did not request it, you can safely ignore this email.
            </p>
            """;

        string html = WrapInLayout("Your Housing Hub admin login code", body, Hero("&#128272;", "Your login code"));
        string text = $"Hi {firstName}, your Housing Hub Admin login code is {otpCode}. It expires in 10 minutes.";

        return await SendAsync(toEmail, "Your Housing Hub Admin login code", text, html);
    }

    public async Task<bool> SendKycApprovedAsync(string toEmail, string firstName)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {firstName},")}
            {P("Your identity verification has been reviewed and approved. Your account is now fully unlocked &mdash; you can book inspections and transact on Housing Hub.")}
            {DetailRow("Verification status", "Approved")}
            {Button("Go to Dashboard", $"{baseUrl}/dashboard")}
            """;

        string html = WrapInLayout("Your KYC has been approved", body, Hero("&#10003;", "Verification approved"));
        string text = $"Hi {firstName}, your Housing Hub KYC verification has been approved. You now have full access to your account.";

        return await SendAsync(toEmail, "Your Housing Hub KYC Has Been Approved", text, html);
    }

    public async Task<bool> SendKycRejectedAsync(string toEmail, string firstName, string reason)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {firstName},")}
            {P("We reviewed your identity verification and were unable to approve it this time. You can correct the issue below and resubmit &mdash; there's no limit on attempts.")}
            {DetailRow("Reason", reason)}
            {Button("Resubmit Documents", $"{baseUrl}/kyc/personal-info")}
            """;

        string html = WrapInLayout("Your KYC was not approved", body, Hero("&#33;", "Verification not approved"));
        string text = $"Hi {firstName}, your Housing Hub KYC verification was not approved. Reason: {reason}. Please log in to review and resubmit your documents.";

        return await SendAsync(toEmail, "Your Housing Hub KYC Was Not Approved", text, html);
    }

    public async Task<bool> SendVerificationApprovedAsync(
        string toEmail, string firstName, string subjectDescription)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {firstName},")}
            {P("We've finished reviewing your documents and everything checks out. Your verification badge is now live, and people browsing Housing Hub can see it.")}
            {DetailRow("Verified", subjectDescription)}
            {Button("View Your Profile", $"{baseUrl}/profile")}
            """;

        string html = WrapInLayout("Verification approved", body, Hero("&#10003;", "Verification approved"));
        string text = $"Hi {firstName}, your Housing Hub verification for {subjectDescription} has been approved.";

        return await SendAsync(toEmail, "Your Housing Hub Verification Has Been Approved", text, html);
    }

    public async Task<bool> SendVerificationRejectedAsync(
        string toEmail, string firstName, string subjectDescription, string reason)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        // The reviewer's note is the whole point of this email. Everything else is
        // packaging — if the applicant cannot tell what to fix, they will email
        // support instead, which costs more than the review did.
        string body = $"""
            {P($"Hi {firstName},")}
            {P("We reviewed your documents and weren't able to approve them this time. The reason is below &mdash; you can correct it and submit again, and there's no limit on attempts.")}
            {DetailRow("Submission", subjectDescription)}
            {DetailRow("What needs fixing", reason)}
            {Button("Review and Resubmit", $"{baseUrl}/verification")}
            """;

        string html = WrapInLayout("Verification not approved", body, Hero("&#33;", "Verification not approved"));
        string text = $"Hi {firstName}, your Housing Hub verification for {subjectDescription} was not approved. Reason: {reason}. You can correct this and resubmit.";

        return await SendAsync(toEmail, "Your Housing Hub Verification Needs Attention", text, html);
    }

    public async Task<bool> SendVerificationExpiredAsync(
        string toEmail, string firstName, string subjectDescription)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        // Careful not to sound like a rejection. Nothing was wrong with what they
        // sent; a date passed. The instruction is "upload a current one", not
        // "correct your mistake".
        string body = $"""
            {P($"Hi {firstName},")}
            {P("One of the documents behind your Housing Hub verification has reached its expiry date, so the badge has come down for now. Nothing was wrong with your submission &mdash; documents like LASRERA registrations simply need renewing each year.")}
            {DetailRow("Affected", subjectDescription)}
            {P("Upload a current document and we&rsquo;ll review it and put the badge back.")}
            {Button("Renew Verification", $"{baseUrl}/verification")}
            """;

        string html = WrapInLayout("Your verification has expired", body, Hero("&#8635;", "Verification expired"));
        string text = $"Hi {firstName}, your Housing Hub verification for {subjectDescription} has expired because a document reached its expiry date. Upload a current document to restore it.";

        return await SendAsync(toEmail, "Your Housing Hub Verification Has Expired", text, html);
    }

    public async Task<bool> SendAccountReactivatedAsync(string toEmail, string firstName)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {firstName},")}
            {P("Your Housing Hub account has been reactivated. You can log in and pick up right where you left off.")}
            {Button("Log In", $"{baseUrl}/login")}
            """;

        string html = WrapInLayout("Your account has been reactivated", body, Hero("&#10003;", "Account reactivated"));
        string text = $"Hi {firstName}, your Housing Hub account has been reactivated. You can now log in and use the platform as usual.";

        return await SendAsync(toEmail, "Your Housing Hub Account Has Been Reactivated", text, html);
    }

    public async Task<bool> SendPropertyVerifiedAsync(string ownerEmail, string ownerName, string propertyTitle)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {ownerName},")}
            {P("Your listing has been reviewed and marked as verified. It now carries a trust badge that prospective tenants can see &mdash; verified listings receive noticeably more inspection requests.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Status", "Verified")}
            {Button("View Listing", $"{baseUrl}/properties")}
            """;

        string html = WrapInLayout("Your property has been verified", body, Hero("&#10003;", "Listing verified"));
        string text = $"Hi {ownerName}, your listing \"{propertyTitle}\" has been verified on Housing Hub.";

        return await SendAsync(ownerEmail, $"Your Property \"{propertyTitle}\" Has Been Verified", text, html);
    }

    public async Task<bool> SendPropertyUnpublishedAsync(string ownerEmail, string ownerName, string propertyTitle, string reason)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {ownerName},")}
            {P("Your listing has been unpublished by an administrator and is no longer visible to the public. Resolve the issue below and you can republish it from your dashboard.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Reason", reason)}
            {Button("Manage Listing", $"{baseUrl}/properties")}
            """;

        string html = WrapInLayout("Your property has been unpublished", body, Hero("&#128065;", "Listing unpublished"));
        string text = $"Hi {ownerName}, your listing \"{propertyTitle}\" has been unpublished by a Housing Hub administrator. Reason: {reason}.";

        return await SendAsync(ownerEmail, $"Your Property \"{propertyTitle}\" Has Been Unpublished", text, html);
    }

    public async Task<bool> SendPropertyDeletedAsync(string ownerEmail, string ownerName, string propertyTitle, string reason)
    {
        string body = $"""
            {P($"Hi {ownerName},")}
            {P("Your listing has been removed from Housing Hub by an administrator.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Reason", reason)}
            {Callout("Think this is a mistake?", "Reply is not monitored on this address &mdash; please contact our team at info@housinghub.ng and we'll review it.")}
            """;

        string html = WrapInLayout("Your property has been removed", body, Hero("&#10005;", "Listing removed"));
        string text = $"Hi {ownerName}, your listing \"{propertyTitle}\" has been removed from Housing Hub by an administrator. Reason: {reason}.";

        return await SendAsync(ownerEmail, $"Your Property \"{propertyTitle}\" Has Been Removed", text, html);
    }

    public async Task<bool> SendInspectionHandoffToAdminsAsync(string adminEmail, string adminFirstName, string ownerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime)
    {
        string adminBaseUrl = _configuration["Email:AdminBaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {adminFirstName},")}
            {P($"<strong style=\"color:{Ink};\">{ownerName}</strong> has handed an inspection request off to HousingHub instead of managing it directly. It needs a staff member assigned.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Owner", ownerName)}
            {DetailRow("Date &amp; time", $"{scheduledDate:dddd, d MMMM yyyy} &middot; {scheduledTime:hh\\:mm}")}
            {Button("Assign a Staff Member", $"{adminBaseUrl}/admin/inspections")}
            """;

        string html = WrapInLayout("Inspection handed off to HousingHub", body, Hero("&#128203;", "Needs staff assignment"));
        string text = $"Hi {adminFirstName}, {ownerName} handed an inspection for {propertyTitle} off to HousingHub. Log in to assign a staff member.";

        return await SendAsync(adminEmail, $"Inspection Handed Off — {propertyTitle}", text, html);
    }

    public async Task<bool> SendStaffAssignedToInspectionAsync(string staffEmail, string staffFirstName, string propertyTitle, string ownerName, DateTime scheduledDate, TimeSpan scheduledTime)
    {
        string adminBaseUrl = _configuration["Email:AdminBaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {staffFirstName},")}
            {P("You've been assigned to manage an inspection that was handed off to HousingHub. You can confirm, decline, or reschedule it from your dashboard.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Owner", ownerName)}
            {DetailRow("Date &amp; time", $"{scheduledDate:dddd, d MMMM yyyy} &middot; {scheduledTime:hh\\:mm}")}
            {Button("View Inspection", $"{adminBaseUrl}/admin/inspections")}
            """;

        string html = WrapInLayout("You've been assigned an inspection", body, Hero("&#128100;", "New assignment"));
        string text = $"Hi {staffFirstName}, you've been assigned to manage the inspection for {propertyTitle}. Log in to your dashboard for details.";

        return await SendAsync(staffEmail, $"You've Been Assigned an Inspection — {propertyTitle}", text, html);
    }

    public async Task<bool> SendPropertyAlertMatchAsync(string customerEmail, string customerFirstName, string propertyTitle, string propertyAddress, decimal price)
    {
        string baseUrl = _configuration["Email:BaseUrl"] ?? "https://localhost";

        string body = $"""
            {P($"Hi {customerFirstName},")}
            {P("A new listing just went live that matches one of your saved searches.")}
            {DetailRow("Property", propertyTitle)}
            {DetailRow("Location", propertyAddress)}
            {DetailRow("Price", $"&#8358;{price:N0}")}
            {Button("View Listing", $"{baseUrl}/properties")}
            """;

        string html = WrapInLayout("A property matching your search just went live", body, Hero("&#128269;", "New match found"));
        string text = $"Hi {customerFirstName}, a new listing matching your saved search just went live: {propertyTitle} at {propertyAddress}. Log in to Housing Hub to view it.";

        return await SendAsync(customerEmail, $"New Match: {propertyTitle}", text, html);
    }

    // ── Brand tokens ─────────────────────────────────────────────────────────
    // Navy carries the structure; gold is an accent only (~10-15% of any view).
    private const string Navy = "#0B2545";
    private const string NavyDeep = "#071A33";
    private const string Gold = "#C9A227";
    private const string HeroTint = "#E9F0F9";
    private const string Ink = "#1F2937";
    private const string Muted = "#5A6B7F";
    private const string Canvas = "#F4F6F9";
    private const string Font = "'Helvetica Neue',Helvetica,Arial,sans-serif";

    /// <summary>
    /// The hero band that sits directly under the brand header — a circular glyph
    /// over a tinted panel, stating in one line what the email is about.
    /// </summary>
    private static string Hero(string glyph, string heading)
    {
        return $"""
            <tr>
              <td style="background-color:{HeroTint};padding:32px 40px 28px;text-align:center;">
                <table role="presentation" cellspacing="0" cellpadding="0" border="0" align="center" style="margin:0 auto 14px;">
                  <tr>
                    <td width="52" height="52" align="center" valign="middle" style="width:52px;height:52px;background-color:{Navy};border-radius:26px;font-size:24px;line-height:52px;color:{Gold};font-family:{Font};">{glyph}</td>
                  </tr>
                </table>
                <h1 style="margin:0;font-size:21px;font-weight:800;color:{Navy};font-family:{Font};letter-spacing:-0.2px;">{heading}</h1>
              </td>
            </tr>
            """;
    }

    /// <summary>
    /// A scannable key/value row with a gold rule down the left edge. Used for the
    /// data-heavy mails (inspections, listings) where the detail is the payload.
    /// </summary>
    private static string DetailRow(string label, string value)
    {
        return $"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:10px;">
              <tr>
                <td style="background-color:#F7F8FA;border-left:3px solid {Gold};padding:12px 16px;">
                  <p style="margin:0 0 3px 0;font-size:11px;font-weight:700;color:#9AA3AE;text-transform:uppercase;letter-spacing:0.7px;font-family:{Font};">{label}</p>
                  <p style="margin:0;font-size:15px;font-weight:700;color:{Navy};font-family:{Font};">{value}</p>
                </td>
              </tr>
            </table>
            """;
    }

    /// <summary>Primary call to action. Gold on navy text — highest contrast pairing in the palette.</summary>
    private static string Button(string label, string href)
    {
        return $"""
            <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="margin:24px 0 4px;">
              <tr>
                <td style="border-radius:6px;background-color:{Gold};">
                  <a href="{href}" style="display:inline-block;padding:14px 34px;font-size:15px;font-weight:800;color:{Navy};text-decoration:none;border-radius:6px;font-family:{Font};letter-spacing:0.2px;">{label}</a>
                </td>
              </tr>
            </table>
            """;
    }

    /// <summary>Secondary/cautionary callout — used for security notices.</summary>
    private static string Callout(string heading, string text)
    {
        return $"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:20px 0;">
              <tr>
                <td style="background-color:#FFF9E8;border-left:3px solid {Gold};padding:14px 18px;">
                  <p style="margin:0 0 4px 0;font-size:14px;font-weight:800;color:#6B5309;font-family:{Font};">{heading}</p>
                  <p style="margin:0;font-size:13px;color:#8A6D10;line-height:1.6;font-family:{Font};">{text}</p>
                </td>
              </tr>
            </table>
            """;
    }

    /// <summary>Standard body paragraph.</summary>
    private static string P(string text) =>
        $"""<p style="margin:0 0 14px 0;font-size:15px;color:{Muted};line-height:1.65;font-family:{Font};">{text}</p>""";

    /// <summary>
    /// Wraps a body-content HTML fragment in the shared Housing Hub email shell.
    /// Structured Card layout: navy brand header, optional tinted hero band,
    /// white content well, navy footer. Table-based and inline-styled throughout
    /// so it survives Outlook and Gmail's CSS stripping.
    /// </summary>
    /// <param name="title">Document title / preheader.</param>
    /// <param name="bodyHtml">The content well markup.</param>
    /// <param name="heroHtml">Optional hero band, produced by <see cref="Hero"/>.</param>
    private string WrapInLayout(string title, string bodyHtml, string heroHtml = "")
    {
        int year = DateTime.UtcNow.Year;

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width,initial-scale=1.0">
              <meta name="color-scheme" content="light">
              <title>{title}</title>
            </head>
            <body style="margin:0;padding:0;background-color:{Canvas};font-family:{Font};-webkit-font-smoothing:antialiased;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;">{title}</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:{Canvas};">
                <tr><td align="center" style="padding:40px 16px;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:560px;border-radius:12px;overflow:hidden;box-shadow:0 2px 18px rgba(11,37,69,0.10);">

                    <!-- Brand header: architectural H mark + wordmark -->
                    <tr>
                      <td style="background-color:{Navy};padding:20px 32px;">
                        <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                          <tr>
                            <td valign="middle" style="padding-right:11px;">
                              <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                                <tr>
                                  <td width="26" height="26" align="center" valign="middle" style="width:26px;height:26px;background-color:{Gold};border-radius:6px;font-size:15px;font-weight:800;line-height:26px;color:{Navy};font-family:{Font};">H</td>
                                </tr>
                              </table>
                            </td>
                            <td valign="middle">
                              <span style="font-size:17px;font-weight:800;color:#ffffff;font-family:{Font};letter-spacing:-0.2px;">Housing</span><span style="font-size:17px;font-weight:400;color:{Gold};font-family:{Font};letter-spacing:1.4px;"> HUB</span>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
            {heroHtml}
                    <!-- Content well -->
                    <tr>
                      <td style="background-color:#ffffff;padding:32px 40px 36px;">
                        {bodyHtml}
                      </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                      <td style="background-color:{NavyDeep};padding:22px 40px;text-align:center;">
                        <p style="margin:0 0 5px 0;font-size:12px;color:rgba(255,255,255,0.66);font-family:{Font};">&copy; {year} Housing Hub &middot; Lagos, Nigeria</p>
                        <p style="margin:0;font-size:11px;color:rgba(255,255,255,0.38);font-family:{Font};">Automated message &mdash; please do not reply to this email.</p>
                      </td>
                    </tr>

                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private async Task<bool> SendAsync(string toEmail, string subject, string text, string html)
    {
        try
        {
            string fromEmail = _configuration["Email:SenderEmail"]!;
            string fromName = _configuration["Email:SenderName"] ?? "Housing Hub";
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
