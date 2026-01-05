using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyFile;

namespace HousingHub.Service.PropertyFileService.Interfaces;

public interface IPropertyFileCommandService
{
    Task<BaseResponse<PropertyFileDto>> CreatePropertyFile(CreatePropertyFileDto request);
}
