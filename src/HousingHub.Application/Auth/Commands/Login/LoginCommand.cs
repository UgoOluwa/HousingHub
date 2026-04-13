using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.Login;

public record LoginCommand(string EmailOrPhone, string Password) : IRequest<BaseResponse<LoginCustomerResponseDto?>>;
