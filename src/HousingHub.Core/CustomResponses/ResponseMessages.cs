namespace HousingHub.Core.CustomResponses;

public static class ResponseMessages
{
    public const string Successful = "Success.";
    public const string Failed = "Something went wrong. Please try again.";

    /// <summary>
    /// Client-facing message for an unhandled exception.
    /// </summary>
    /// <remarks>
    /// Services previously returned ex.Message directly, which leaks internals —
    /// table names, driver errors, stack-adjacent detail — to any caller who can
    /// trigger a failure. The full exception is still logged server-side.
    /// </remarks>
    public const string UnexpectedError = "Something went wrong on our end. Please try again.";
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
    public const string InvalidRefreshToken = "Invalid or expired refresh token. Please log in again.";
    public const string OtpSent = "If that email is registered, a login code has been sent.";
    public const string OtpInvalidOrExpired = "Invalid or expired code. Please check the code or request a new one.";
    public const string OtpTooManyAttempts = "Too many incorrect attempts. Please request a new code.";
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
    public const string NoFileProvided = "No file was provided. Please attach a document and try again.";

    // Property messages
    public const string UnauthorizedPropertyAction = "Only home owners or agents can manage properties.";
    public const string PropertyNotOwnedByUser = "You do not have permission to modify this property.";
    public const string FileTooLarge = "File size exceeds the maximum allowed size of 10MB.";
    public const string InvalidFileType = "Only image and video files are allowed.";
    public const string OwnerNotManagedByHousingHub = "This owner isn't managed by HousingHub, so an admin can't post a listing on their behalf.";

    /// <summary>
    /// Shown when an owner tries to publish before their identity has been verified.
    /// Deliberately explains the state and the way out of it — this is a legitimate
    /// user doing the right thing slightly early, not an attacker.
    /// </summary>
    public const string KycRequiredToPublish = "Your identity is still being verified. You can keep editing this listing, and you'll be able to publish it as soon as verification is complete.";

    /// <summary>Shown when the owner hasn't submitted identity documents at all yet.</summary>
    public const string KycNotSubmitted = "Please complete identity verification before publishing a listing. You can keep editing it in the meantime.";

    // ── Verification pipeline ────────────────────────────────────

    public const string VerificationCaseNotFound = "We couldn't find that verification request.";
    public const string VerificationCaseNotEditable = "This verification request has already been submitted, so its documents can't be changed. Contact support if something needs correcting.";
    public const string VerificationCaseAlreadySubmitted = "This verification request has already been submitted.";
    public const string VerificationCaseNotAwaitingReview = "This verification request isn't awaiting a decision.";
    public const string VerificationDecisionNoteRequired = "A reason is required so the applicant knows what to do next.";
    public const string VerificationDocumentsNotAllReviewed = "Every document must be reviewed before the case can be approved.";
    public const string VerificationDocumentRejectionReasonRequired = "A reason is required when rejecting a document.";
    public const string VerificationSubjectRequired = "A property must be specified for a title verification request.";
    public const string VerificationPropertyNotOwned = "You can only request verification for your own listing.";

    public static string VerificationDocumentsMissing(IEnumerable<string> documentNames) =>
        $"Please attach the following before submitting: {string.Join(", ", documentNames)}.";

    // Inspection messages
    public const string InspectionNotPending = "Only pending inspections can be accepted or declined.";
    public const string InspectionNotPendingOrRescheduled = "Only pending or rescheduled inspections can be responded to.";
    public const string InspectionNotOwner = "Only the property owner can respond to inspections.";
    public const string InspectionNotCustomer = "Only the customer who scheduled the inspection can respond to a reschedule.";
    public const string InspectionNotParticipant = "Only the property owner or the customer who scheduled the inspection can reschedule.";
    public const string InspectionCannotReschedule = "Only pending or confirmed inspections can be rescheduled.";
    public const string CannotInspectOwnProperty = "You cannot schedule an inspection for your own property.";
    public const string InspectionAlreadyPending = "You already have a pending inspection request for this property.";
    public const string InspectionAlreadyHandedOff = "This inspection has already been handed off to HousingHub.";
    public const string InspectionCannotHandOff = "Completed, declined, or cancelled inspections can't be handed off.";
    public const string InspectionNotHandedOff = "This inspection hasn't been handed off to HousingHub yet, so it can't be assigned to staff.";
    public const string CannotReportOwnProperty = "You cannot report your own property.";
    public const string PropertyReportSubmitted = "Thanks — your report has been submitted for review.";


    // ── Chat ─────────────────────────────────────────────────────

    public const string ChatCannotMessageSelf = "You cannot send a message to yourself.";
    public const string ChatNotParticipant = "You are not a participant in this conversation.";

    /// <summary>
    /// The "sender" is the caller's own account, so naming it as a sender is
    /// meaningless to them — they did not know they were one. An admin messaging
    /// from the dashboard hits this path too, hence "your account" rather than
    /// anything role-specific.
    /// </summary>
    public const string ChatSenderNotFound = "We couldn't find your account. Please sign in again.";

    public const string ChatRecipientNotFound = "We couldn't find the person you're trying to message.";

    // ── Notifications ────────────────────────────────────────────

    public const string NotificationNotOwned = "You can only mark your own notifications as read.";

    // ── KYC documents ────────────────────────────────────────────

    public const string KycDocumentNotOnFile = "No identity document has been uploaded for this account.";
    public const string KycDocumentUploaded = "Document uploaded successfully.";
    public const string KycSubmitted = "Your documents have been submitted. We'll let you know once verification is complete.";
    public const string InvalidCustomerId = "That customer reference isn't valid.";

    /// <summary>
    /// Shown beside a freshly minted presigned URL. The ten minutes is the
    /// expiry in <c>IFileStorageService</c>; if that changes, this changes.
    /// </summary>
    public const string PresignedLinkValidity = "Link valid for 10 minutes.";

    // ── Customer administration ──────────────────────────────────

    public const string CustomerSuspended = "Customer suspended.";
    public const string CustomerReactivated = "Customer reactivated.";

    // ── Property administration ──────────────────────────────────

    public const string DuplicateFlagDismissed = "Duplicate flag dismissed.";

    // ── Admin accounts ───────────────────────────────────────────

    public const string AdminProfileUpdated = "Profile updated.";
    public const string AdminPasswordIncorrectOrNotFound = "Current password is incorrect, or that admin no longer exists.";
    public const string StaffMemberCreated = "Staff member created.";
    public const string StaffAccountDeactivated = "Staff account deactivated.";
    public const string StaffAccountReactivated = "Staff account reactivated.";
    public const string CannotDeactivateOwnAccount = "You cannot deactivate your own account.";
    public const string AdminPromotedToSuperAdmin = "Admin promoted to SuperAdmin.";

    // ── Inspections (continued) ──────────────────────────────────

    public const string InspectionCannotCancel = "Completed or already cancelled inspections can't be cancelled.";

    /// <summary>Worker summary. Pluralised properly rather than emitting "reminder(s)".</summary>
    public static string InspectionRemindersSent(int count) =>
        count == 1 ? "Sent 1 inspection reminder." : $"Sent {count} inspection reminders.";

    // ── Generic CRUD templates ───────────────────────────────────
    //
    // Roughly 160 call sites render their messages through these, which is why
    // they are worth getting right: they are the most-seen copy in the product.
    //
    // Every one of them used to emit an ungrammatical fragment -- "customer Not
    // Found", "Successfully created property file" -- built from a lowercase
    // entity noun and a Title-Cased verb. Call sites also disagreed on casing,
    // so "property" and "Property" produced two different messages for one
    // condition.
    //
    // The entity noun is normalised here rather than trusted to agree across
    // thirty-odd call sites, because it did not.

    /// <summary>Lowercases an entity noun for use mid-sentence.</summary>
    private static string Entity(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "record" : name.Trim().ToLowerInvariant();

    /// <summary>Sentence-initial form of an entity noun.</summary>
    private static string EntityCapitalised(string? name)
    {
        var entity = Entity(name);
        return char.ToUpperInvariant(entity[0]) + entity[1..];
    }

    public static string SetCreationSuccessMessage(string message) =>
        $"{EntityCapitalised(message)} created.";

    public static string SetUpdateSuccessMessage(string message) =>
        $"{EntityCapitalised(message)} updated.";

    public static string SetDeletedSuccessMessage(string message) =>
        $"{EntityCapitalised(message)} deleted.";

    public static string SetCreationFailureMessage(string message) =>
        $"We couldn't create that {Entity(message)}. Please try again.";

    public static string SetUpdateFailureMessage(string message) =>
        $"We couldn't update that {Entity(message)}. Please try again.";

    public static string SetDeletedFailureMessage(string message) =>
        $"We couldn't delete that {Entity(message)}. Please try again.";

    public static string SetNotFoundMessage(string message) =>
        $"We couldn't find that {Entity(message)}.";

    public static string SetAlreadyExistsMessage(string message) =>
        $"That {Entity(message)} already exists.";
} 
