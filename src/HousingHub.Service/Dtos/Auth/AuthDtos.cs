namespace HousingHub.Service.Dtos.Auth;

public record VerifyEmailRequestDto(string Email, string Token);

public record ForgotPasswordRequestDto(string Email);

public record ResetPasswordRequestDto(string Email, string Token, string NewPassword);

public record ChangePasswordRequestDto(Guid CustomerId, string CurrentPassword, string NewPassword);

public record ChangePasswordBodyDto(string CurrentPassword, string NewPassword);

public record GoogleSignInRequestDto(string IdToken);

public record GoogleClaimsDto(string Email, string GoogleId, string? FirstName, string? LastName);
