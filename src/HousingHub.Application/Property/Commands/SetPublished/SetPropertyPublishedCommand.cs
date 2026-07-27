using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Property.Commands.SetPublished;

/// <summary>
/// Owner publishes or unpublishes their own listing. AuthenticatedUserId comes
/// from the JWT and is used to enforce ownership.
/// </summary>
public record SetPropertyPublishedCommand(Guid PropertyId, bool IsPublished, Guid AuthenticatedUserId = default)
    : IRequest<BaseResponse<bool>>;
