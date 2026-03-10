using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Auth;
using MediatR;

namespace HousingHub.Application.Auth.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, BaseResponse<bool>>
{
    private readonly IAuthService _authService;

    public ChangePasswordCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<BaseResponse<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.ChangePassword(
            new ChangePasswordRequestDto(request.CustomerId, request.CurrentPassword, request.NewPassword));
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
