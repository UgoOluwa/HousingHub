using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<BaseResponse<LoginCustomerResponseDto?>>;
