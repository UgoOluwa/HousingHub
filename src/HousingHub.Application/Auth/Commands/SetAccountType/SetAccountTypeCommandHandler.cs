using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.SetAccountType;

public class SetAccountTypeCommandHandler
    : IRequestHandler<SetAccountTypeCommand, BaseResponse<LoginCustomerResponseDto?>>
{
    private readonly IAuthService _authService;

    public SetAccountTypeCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<BaseResponse<LoginCustomerResponseDto?>> Handle(
        SetAccountTypeCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.SetAccountType(request.CustomerId, request.CustomerType);
        return new BaseResponse<LoginCustomerResponseDto?>(
            response.IsSuccessful, response?.Data, response?.Message, null);
    }
}
