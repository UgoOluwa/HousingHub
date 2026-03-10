using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Auth;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Service.AuthService.Interfaces;

public interface IAuthService
{
    Task<BaseResponse<CustomerDto>> Register(RegisterCustomerDto request);
    Task<BaseResponse<LoginCustomerResponseDto>> Login(LoginCustomerDto request);
    Task<BaseResponse<bool>> VerifyEmail(VerifyEmailRequestDto request);
    Task<BaseResponse<string>> ForgotPassword(ForgotPasswordRequestDto request);
    Task<BaseResponse<bool>> ResetPassword(ResetPasswordRequestDto request);
    Task<BaseResponse<bool>> ChangePassword(ChangePasswordRequestDto request);
    Task<BaseResponse<LoginCustomerResponseDto>> GoogleSignIn(GoogleSignInRequestDto request);
    Task<BaseResponse<LoginCustomerResponseDto>> GoogleSignInFromClaims(GoogleClaimsDto claims);
}
