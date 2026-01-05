using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyInterest;

namespace HousingHub.Service.PropertyInterestService.Interfaces;

public interface IPropertyInterestCommandService
{
    Task<BaseResponse<PropertyInterestDto>> CreatePropertyInterest(CreatePropertyInterestDto request);
}
