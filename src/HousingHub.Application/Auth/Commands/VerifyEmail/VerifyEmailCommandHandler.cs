using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Auth;
using MediatR;

namespace HousingHub.Application.Auth.Commands.VerifyEmail;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, BaseResponse<bool>>
{
    private readonly IAuthService _authService;

    public VerifyEmailCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<BaseResponse<bool>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.VerifyEmail(new VerifyEmailRequestDto(request.Email, request.Token));
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
