using AutoMapper;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, BaseResponse<LoginCustomerResponseDto?>>
{
    private readonly IAuthService _authService;
    private readonly IMapper _mapper;

    public LoginCommandHandler(IAuthService authService, IMapper mapper)
    {
        _authService = authService;
        _mapper = mapper;
    }

    public async Task<BaseResponse<LoginCustomerResponseDto?>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.Login(_mapper.Map<LoginCustomerDto>(request));
        return new BaseResponse<LoginCustomerResponseDto?>(response.IsSuccessful, response?.Data, response?.Message, null);
    }
}
