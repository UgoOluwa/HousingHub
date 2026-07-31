namespace HousingHub.Service.Commons.Email;

public interface IEmailService
{
    Task<bool> SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken);
    Task<bool> SendPasswordResetAsync(string toEmail, string firstName, string resetToken);
    /// <summary>Security notice sent after a password is successfully changed/reset.</summary>
    Task<bool> SendPasswordChangedAsync(string toEmail, string firstName);
    Task<bool> SendInspectionScheduledAsync(string ownerEmail, string ownerName, string customerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime, string? note);
    /// <summary>Sent to the customer confirming their inspection request was submitted, pending the owner's response.</summary>
    Task<bool> SendInspectionBookingConfirmationAsync(string customerEmail, string customerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime, string? note);
    Task<bool> SendInspectionResponseAsync(string customerEmail, string customerName, string ownerName, string propertyTitle, string action, string? note, DateTime? rescheduledDate, TimeSpan? rescheduledTime);
    /// <summary>Sent to a chat participant when they receive a new message while offline/unread.</summary>
    Task<bool> SendNewMessageAsync(string recipientEmail, string recipientName, string senderName, string messagePreview);
    /// <summary>Sent to both the owner and the customer ~24 hours before a confirmed inspection.</summary>
    Task<bool> SendInspectionReminderAsync(string recipientEmail, string recipientName, string otherPartyName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime);
    /// <summary>One-time login code for the admin dashboard's OTP-only sign-in.</summary>
    Task<bool> SendAdminOtpAsync(string toEmail, string firstName, string otpCode);
}
