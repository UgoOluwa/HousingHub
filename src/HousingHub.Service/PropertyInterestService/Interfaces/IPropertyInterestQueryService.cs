using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyInterest;

namespace HousingHub.Service.PropertyInterestService.Interfaces;

public interface IPropertyInterestQueryService
{
    Task<BaseResponse<PropertyInterestDto?>> GetPropertyInterestAsync(Guid id);
    Task<BaseResponse<List<PropertyInterestDto>>> GetAllPropertyInterestsAsync(Guid propertyId);
}
