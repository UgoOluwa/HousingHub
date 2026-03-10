using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Auth.Commands.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest<BaseResponse<string?>>;
