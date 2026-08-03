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
    Task<bool> SendKycApprovedAsync(string toEmail, string firstName);
    Task<bool> SendKycRejectedAsync(string toEmail, string firstName, string reason);
    Task<bool> SendAccountReactivatedAsync(string toEmail, string firstName);
    /// <summary>Sent to the property owner when an admin marks their listing verified.</summary>
    Task<bool> SendPropertyVerifiedAsync(string ownerEmail, string ownerName, string propertyTitle);
    /// <summary>Sent to the property owner when an admin unpublishes their listing.</summary>
    Task<bool> SendPropertyUnpublishedAsync(string ownerEmail, string ownerName, string propertyTitle, string reason);
    /// <summary>Sent to the property owner when an admin deletes their listing.</summary>
    Task<bool> SendPropertyDeletedAsync(string ownerEmail, string ownerName, string propertyTitle, string reason);
    /// <summary>Sent to each active SuperAdmin when a property owner hands an inspection off to HousingHub.</summary>
    Task<bool> SendInspectionHandoffToAdminsAsync(string adminEmail, string adminFirstName, string ownerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime);
    /// <summary>Sent to a staff member when a SuperAdmin assigns them a handed-off inspection.</summary>
    Task<bool> SendStaffAssignedToInspectionAsync(string staffEmail, string staffFirstName, string propertyTitle, string ownerName, DateTime scheduledDate, TimeSpan scheduledTime);
}
