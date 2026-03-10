using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyFile;
using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.PropertyFileService.Interfaces;

public interface IPropertyFileCommandService
{
    Task<BaseResponse<PropertyFileDto>> CreatePropertyFile(CreatePropertyFileDto request);
    Task<BaseResponse<List<PropertyFileDto>>> UploadPropertyFiles(Guid propertyId, Guid authenticatedUserId, IList<IFormFile> files);
    Task<BaseResponse<bool>> DeletePropertyFile(Guid fileId, Guid authenticatedUserId);
}
