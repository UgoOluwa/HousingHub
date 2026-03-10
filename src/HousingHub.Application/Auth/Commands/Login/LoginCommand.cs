using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<BaseResponse<LoginCustomerResponseDto?>>;
