using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyAlert;

namespace HousingHub.Service.PropertyAlertService.Interfaces;

public interface IPropertyAlertPreferenceCommandService
{
    Task<BaseResponse<PropertyAlertPreferenceDto>> CreateAsync(Guid customerId, CreatePropertyAlertPreferenceDto request);

    /// <summary>Deletes a saved search — only the customer who created it can delete it.</summary>
    Task<BaseResponse<bool>> DeleteAsync(Guid preferenceId, Guid customerId);
}
