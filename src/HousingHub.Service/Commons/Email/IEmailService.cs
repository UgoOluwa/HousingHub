namespace HousingHub.Service.Commons.Email;

public interface IEmailService
{
    Task<bool> SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken);
    Task<bool> SendPasswordResetAsync(string toEmail, string firstName, string resetToken);

    /// <summary>
    /// Sent when someone attempts to register with an address that already has an
    /// account. Registration returns the same response either way so it cannot be
    /// used to test whether an address is registered; this email is how the real
    /// account holder finds out, and how a legitimate user who forgot they had an
    /// account gets back in.
    /// </summary>
    Task<bool> SendRegistrationAttemptOnExistingAccountAsync(string toEmail, string firstName);
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

    /// <summary>
    /// Tells an applicant their business or property verification was approved.
    /// </summary>
    /// <param name="subjectDescription">"your agency" or the listing's title.</param>
    Task<bool> SendVerificationApprovedAsync(string toEmail, string firstName, string subjectDescription);

    /// <summary>
    /// Tells an applicant their verification was not approved, and why.
    /// </summary>
    /// <param name="reason">
    /// The reviewer's note, shown verbatim. Required by the service layer for exactly
    /// this reason: a rejection the applicant cannot act on becomes a support ticket.
    /// </param>
    Task<bool> SendVerificationRejectedAsync(string toEmail, string firstName, string subjectDescription, string reason);

    /// <summary>
    /// Tells someone their verification lapsed because a document expired.
    /// </summary>
    /// <remarks>
    /// Deliberately distinct from the rejection email. Nothing was wrong with the
    /// submission — it aged out — and somebody who reads this as a rejection goes
    /// looking for a mistake they did not make.
    /// </remarks>
    Task<bool> SendVerificationExpiredAsync(string toEmail, string firstName, string subjectDescription);

    /// <summary>
    /// Warns that a verification is about to lapse, while there is still time to act.
    /// </summary>
    /// <param name="daysRemaining">Days until it lapses. Shown, so it must be accurate.</param>
    /// <param name="expiresAt">The date itself, so the reader can diarise it.</param>
    Task<bool> SendVerificationExpiringSoonAsync(
        string toEmail, string firstName, string subjectDescription, int daysRemaining, DateTime expiresAt);
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
    /// <summary>Sent to a customer when a newly published property matches one of their saved search preferences.</summary>
    Task<bool> SendPropertyAlertMatchAsync(string customerEmail, string customerFirstName, string propertyTitle, string propertyAddress, decimal price);
}
