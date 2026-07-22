namespace HousingHub.Service.Dtos.Auth;

public record VerifyEmailRequestDto(string Email, string Token);

public record ForgotPasswordRequestDto(string Email);

public record ResetPasswordRequestDto(string Email, string Token, string NewPassword);

public record ChangePasswordRequestDto(Guid CustomerId, string CurrentPassword, string NewPassword);

public record ChangePasswordBodyDto(string CurrentPassword, string NewPassword);

public record GoogleSignInRequestDto(string IdToken);

/// <summary>
/// EmailVerified comes from Google's `email_verified` claim and gates linking this
/// identity onto an existing account — see AuthService.LinkGoogleIdentity.
/// </summary>
public record GoogleClaimsDto(string Email, string GoogleId, string? FirstName, string? LastName, bool EmailVerified);
