using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Property.Commands.PublishProperty;

public record PublishPropertyCommand(Guid PropertyId, bool IsPublished) : IRequest<BaseResponse<bool>>;
