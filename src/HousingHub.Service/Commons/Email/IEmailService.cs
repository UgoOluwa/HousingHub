namespace HousingHub.Service.Commons.Email;

public interface IEmailService
{
    Task<bool> SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken);
    Task<bool> SendPasswordResetAsync(string toEmail, string firstName, string resetToken);
    Task<bool> SendInspectionScheduledAsync(string ownerEmail, string ownerName, string customerName, string propertyTitle, DateTime scheduledDate, TimeSpan scheduledTime, string? note);
    Task<bool> SendInspectionResponseAsync(string customerEmail, string customerName, string ownerName, string propertyTitle, string action, string? note, DateTime? rescheduledDate, TimeSpan? rescheduledTime);
}
