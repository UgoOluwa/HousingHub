using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Auth.Commands.ChangePassword;

public record ChangePasswordCommand(Guid CustomerId, string CurrentPassword, string NewPassword) : IRequest<BaseResponse<bool>>;
