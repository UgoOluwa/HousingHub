using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Auth;
using MediatR;

namespace HousingHub.Application.Auth.Commands.ResendOtp;

public class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand, BaseResponse<bool>>
{
    private readonly IAuthService _authService;

    public ResendOtpCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<BaseResponse<bool>> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.ResendEmailVerificationToken(request.Email);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
