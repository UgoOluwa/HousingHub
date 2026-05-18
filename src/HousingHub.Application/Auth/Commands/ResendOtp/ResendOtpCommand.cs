using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Auth.Commands.ResendOtp;

public record ResendOtpCommand(string Email) : IRequest<BaseResponse<bool>>;
