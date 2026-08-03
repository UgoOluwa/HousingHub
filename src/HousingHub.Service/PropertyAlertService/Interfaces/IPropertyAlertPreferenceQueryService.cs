using HousingHub.Core.CustomResponses;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyAlert;

namespace HousingHub.Service.PropertyAlertService.Interfaces;

public interface IPropertyAlertPreferenceQueryService
{
    Task<BaseResponse<List<PropertyAlertPreferenceDto>>> GetByCustomerAsync(Guid customerId);

    /// <summary>All active preferences across every customer — used to find matches when a property is published.</summary>
    Task<List<PropertyAlertPreference>> GetAllActiveAsync();
}
