using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Customer.Commands.Delete;

public record DeleteCustomerCommand(Guid Id) : IRequest<BaseResponse<bool>>;
