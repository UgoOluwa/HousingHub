using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.GoogleSignIn;

public record GoogleSignInCommand(string IdToken) : IRequest<BaseResponse<LoginCustomerResponseDto?>>;
