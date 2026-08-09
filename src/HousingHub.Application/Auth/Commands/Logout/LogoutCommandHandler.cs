using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using MediatR;

namespace HousingHub.Application.Auth.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, BaseResponse<bool>>
{
    private readonly IAuthService _authService;

    public LogoutCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<BaseResponse<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.Logout(request.RefreshToken, request.AllSessions);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
