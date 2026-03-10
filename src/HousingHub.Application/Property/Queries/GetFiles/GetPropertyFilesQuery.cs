using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.PropertyFile;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetFiles;

public record GetPropertyFilesQuery(Guid PropertyId) : IRequest<BaseResponse<List<PropertyFileDto>?>>;
