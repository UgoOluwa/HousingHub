using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Auth.Commands.VerifyEmail;

public record VerifyEmailCommand(string Email, string Token) : IRequest<BaseResponse<bool>>;
