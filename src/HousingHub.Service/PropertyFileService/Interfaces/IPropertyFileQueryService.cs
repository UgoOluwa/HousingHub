using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyFile;

namespace HousingHub.Service.PropertyFileService.Interfaces;

public interface IPropertyFileQueryService
{
    Task<BaseResponse<PropertyFileDto?>> GetPropertyFileAsync(Guid id);
    Task<BaseResponse<List<PropertyFileDto>>> GetAllPropertyFilesAsync(Guid propertyId);
}
