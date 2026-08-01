using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, BaseResponse<LoginCustomerResponseDto?>>
{
    private readonly IAuthService _authService;

    public RefreshTokenCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<BaseResponse<LoginCustomerResponseDto?>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshToken(request.RefreshToken);
        return new BaseResponse<LoginCustomerResponseDto?>(response.IsSuccessful, response?.Data, response?.Message, null);
    }
}
