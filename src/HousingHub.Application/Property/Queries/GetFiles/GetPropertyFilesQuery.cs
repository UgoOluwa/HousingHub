using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.PropertyFile;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetFiles;

public record GetPropertyFilesQuery(Guid PropertyId, Guid? RequestingUserId = null) : IRequest<BaseResponse<List<PropertyFileDto>?>>;
