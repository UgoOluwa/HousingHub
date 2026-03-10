using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<BaseResponse<bool>>;
