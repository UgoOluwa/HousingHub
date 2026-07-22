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

    // Account linking
    public const string AccountHasNoPassword = "This account was created with Google. Sign in with Google, or use \"Forgot password\" to set a password.";
    public const string GoogleEmailNotVerified = "Google hasn't verified this email address, so we can't link it to an existing Housing Hub account.";
    public const string GoogleAccountMismatch = "This email is already linked to a different Google account.";
    public const string AccountTypeAlreadySet = "Your account type has already been set.";

    public static string ResendVerificationTooSoon(int secondsRemaining)
    {
        var minutes = secondsRemaining / 60;
        var seconds = secondsRemaining % 60;
        var wait = minutes > 0 ? $"{minutes}m {seconds:D2}s" : $"{seconds}s";
        return $"A verification link was just sent. Please wait {wait} before requesting another.";
    }

    public const string InvalidAccountType = "Choose a valid account type.";

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
    public const string InspectionNotParticipant = "Only the property owner or the customer who scheduled the inspection can reschedule.";
    public const string InspectionCannotReschedule = "Only pending or confirmed inspections can be rescheduled.";
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
