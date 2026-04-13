using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Customer.Commands.VerifyKyc;

public record VerifyKycCommand(Guid CustomerId, bool IsApproved) : IRequest<BaseResponse<bool>>;
