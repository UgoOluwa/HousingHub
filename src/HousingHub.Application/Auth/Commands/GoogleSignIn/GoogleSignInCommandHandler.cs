using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Auth;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.GoogleSignIn;

public class GoogleSignInCommandHandler : IRequestHandler<GoogleSignInCommand, BaseResponse<LoginCustomerResponseDto?>>
{
    private readonly IAuthService _authService;

    public GoogleSignInCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<BaseResponse<LoginCustomerResponseDto?>> Handle(GoogleSignInCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.GoogleSignIn(new GoogleSignInRequestDto(request.IdToken));
        return new BaseResponse<LoginCustomerResponseDto?>(response.IsSuccessful, response?.Data, response?.Message, null);
    }
}
