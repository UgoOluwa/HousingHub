using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Auth;
using MediatR;

namespace HousingHub.Application.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, BaseResponse<bool>>
{
    private readonly IAuthService _authService;

    public ResetPasswordCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<BaseResponse<bool>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.ResetPassword(new ResetPasswordRequestDto(request.Email, request.Token, request.NewPassword));
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
