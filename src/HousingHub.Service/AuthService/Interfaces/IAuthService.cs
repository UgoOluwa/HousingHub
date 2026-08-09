using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Auth;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Service.AuthService.Interfaces;

public interface IAuthService
{
    Task<BaseResponse<CustomerDto>> Register(RegisterCustomerDto request);
    Task<BaseResponse<LoginCustomerResponseDto>> Login(LoginCustomerDto request);
    Task<BaseResponse<bool>> VerifyEmail(VerifyEmailRequestDto request);
    Task<BaseResponse<int>> ResendEmailVerificationToken(string email);
    Task<BaseResponse<string>> ForgotPassword(ForgotPasswordRequestDto request);
    Task<BaseResponse<bool>> ResetPassword(ResetPasswordRequestDto request);
    Task<BaseResponse<bool>> ChangePassword(ChangePasswordRequestDto request);
    Task<BaseResponse<LoginCustomerResponseDto>> GoogleSignIn(GoogleSignInRequestDto request);
    Task<BaseResponse<LoginCustomerResponseDto>> GoogleSignInFromClaims(GoogleClaimsDto claims);
    Task<BaseResponse<LoginCustomerResponseDto>> SetAccountType(Guid customerId, CustomerType customerType);
    /// <summary>Exchanges a valid, unexpired refresh token for a new access token and a rotated refresh token.</summary>
    Task<BaseResponse<LoginCustomerResponseDto>> RefreshToken(string refreshToken);

    /// <summary>
    /// Revokes the presented refresh token, ending that session server-side.
    /// </summary>
    /// <param name="allSessions">
    /// When true, revokes every active token for the account rather than just this one
    /// — for "sign out everywhere".
    /// </param>
    Task<BaseResponse<bool>> Logout(string refreshToken, bool allSessions = false);

    /// <summary>
    /// Revokes every active refresh token for an account. Called when an admin
    /// suspends the account, so the session cannot outlive the suspension.
    /// </summary>
    Task RevokeAllSessionsAsync(Guid customerId);
}
