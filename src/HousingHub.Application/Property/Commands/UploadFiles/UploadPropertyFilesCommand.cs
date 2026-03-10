using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.PropertyFile;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace HousingHub.Application.Property.Commands.UploadFiles;

public record UploadPropertyFilesCommand(
    Guid PropertyId,
    Guid AuthenticatedUserId,
    IList<IFormFile> Files) : IRequest<BaseResponse<List<PropertyFileDto>?>>;
