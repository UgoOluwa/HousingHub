using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Property.Commands.Delete;

public record DeletePropertyCommand(Guid PropertyId, Guid AuthenticatedUserId) : IRequest<BaseResponse<bool>>;
