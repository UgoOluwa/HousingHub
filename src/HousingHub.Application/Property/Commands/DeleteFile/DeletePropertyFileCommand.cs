using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Property.Commands.DeleteFile;

public record DeletePropertyFileCommand(Guid FileId, Guid AuthenticatedUserId) : IRequest<BaseResponse<bool>>;
