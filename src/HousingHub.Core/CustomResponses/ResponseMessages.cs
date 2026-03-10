namespace HousingHub.Core.CustomResponses;

public static class ResponseMessages
{
    public const string Successful = "Operation Successful";
    public const string Failed = "Operation Failed";
    public const string CustomerAlreadyExists = "Customer with the same email or phone number already exists.";

    // Auth messages
    public const string InvalidCredentials = "Invalid email or password.";
    public const string LoginSuccess = "Login successful.";
    public const string EmailAlreadyVerified = "Email is already verified.";
    public const string EmailNotVerified = "Please verify your email before logging in.";
    public const string EmailVerificationSuccess = "Email verified successfully.";
    public const string EmailVerificationFailed = "Invalid or expired verification token.";
    public const string PasswordResetTokenSent = "If an account with that email exists, a password reset token has been generated.";
    public const string PasswordResetSuccess = "Password reset successfully.";
    public const string PasswordResetFailed = "Invalid or expired reset token.";
    public const string PasswordChangeSuccess = "Password changed successfully.";
    public const string CurrentPasswordIncorrect = "Current password is incorrect.";
    public const string GoogleSignInFailed = "Google sign-in failed. Invalid token.";
    public const string AccountUsesGoogleAuth = "This account uses Google sign-in. Please use Google to log in.";
    public const string AccountUsesLocalAuth = "This account uses email/password sign-in. Please log in with your password.";

    // Property messages
    public const string UnauthorizedPropertyAction = "Only home owners or agents can manage properties.";
    public const string PropertyNotOwnedByUser = "You do not have permission to modify this property.";
    public const string FileTooLarge = "File size exceeds the maximum allowed size of 10MB.";
    public const string InvalidFileType = "Only image and video files are allowed.";

    // Inspection messages
    public const string InspectionNotPending = "Only pending inspections can be accepted or declined.";
    public const string InspectionNotPendingOrRescheduled = "Only pending or rescheduled inspections can be responded to.";
    public const string InspectionNotOwner = "Only the property owner can respond to inspections.";
    public const string InspectionNotCustomer = "Only the customer who scheduled the inspection can respond to a reschedule.";
    public const string CannotInspectOwnProperty = "You cannot schedule an inspection for your own property.";


    public static string SetCreationSuccessMessage(string message)
    {
        string successMessage = $"Successfully created {message}";
        return successMessage;
    }

    public static string SetUpdateSuccessMessage(string message)
    {
        string successMessage = $"Successfully updated {message}";
        return successMessage;
    }

    public static string SetDeletedSuccessMessage(string message)
    {
        string successMessage = $"Successfully deleted {message}";
        return successMessage;
    }

    public static string SetCreationFailureMessage(string message)
    {
        string failureMessage = $"Failed to create {message}";
        return failureMessage;
    }

    public static string SetUpdateFailureMessage(string message)
    {
        string failureMessage = $"Failed to update {message}";
        return failureMessage;
    }

    public static string SetDeletedFailureMessage(string message)
    {
        string failureMessage = $"Failed to deleted {message}";
        return failureMessage;
    }

    public static string SetNotFoundMessage(string message)
    {
        string notFoundMessage = $"{message} Not Found";
        return notFoundMessage;
    }

    public static string SetAlreadyExistsMessage(string message)
    {
        string alreadyExistsMessage = $"The {message} already exists.";
        return alreadyExistsMessage;
    }
} 
