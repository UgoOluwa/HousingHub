using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Auth;
using MediatR;

namespace HousingHub.Application.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, BaseResponse<string?>>
{
    private readonly IAuthService _authService;

    public ForgotPasswordCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<BaseResponse<string?>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.ForgotPassword(new ForgotPasswordRequestDto(request.Email));
        return new BaseResponse<string?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
