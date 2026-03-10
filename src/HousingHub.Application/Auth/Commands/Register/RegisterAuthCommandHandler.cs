using AutoMapper;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.Register;

public class RegisterAuthCommandHandler : IRequestHandler<RegisterAuthCommand, BaseResponse<CustomerDto?>>
{
    private readonly IAuthService _authService;
    private readonly IMapper _mapper;

    public RegisterAuthCommandHandler(IAuthService authService, IMapper mapper)
    {
        _authService = authService;
        _mapper = mapper;
    }

    public async Task<BaseResponse<CustomerDto?>> Handle(RegisterAuthCommand request, CancellationToken cancellationToken)
    {
        var response = await _authService.Register(_mapper.Map<RegisterCustomerDto>(request));
        return new BaseResponse<CustomerDto?>(response.IsSuccessful, response?.Data, response?.Message, null);
    }
}
