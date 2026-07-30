using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Property.Commands.PublishProperty;

public record PublishPropertyCommand(Guid PropertyId, bool IsPublished, Guid AuthenticatedUserId) : IRequest<BaseResponse<bool>>;
