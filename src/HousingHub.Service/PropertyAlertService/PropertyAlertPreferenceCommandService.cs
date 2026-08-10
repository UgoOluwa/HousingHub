using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyAlert;
using HousingHub.Service.PropertyAlertService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyAlertService;

public class PropertyAlertPreferenceCommandService : IPropertyAlertPreferenceCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyAlertPreferenceCommandService> _logger;
    private const string ClassName = "property alert preference";

    public PropertyAlertPreferenceCommandService(IUnitOfWOrk unitOfWOrk, ILogger<PropertyAlertPreferenceCommandService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _logger = logger;
    }

    public async Task<BaseResponse<PropertyAlertPreferenceDto>> CreateAsync(Guid customerId, CreatePropertyAlertPreferenceDto request)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByIdAsync(customerId);
            if (customer == null)
                return new BaseResponse<PropertyAlertPreferenceDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            var preference = new PropertyAlertPreference(
                customerId, request.PropertyType, request.MinPrice, request.MaxPrice,
                request.City, request.State, request.Features);

            bool isSuccessful = await _unitOfWOrk.PropertyAlertPreferenceCommands.InsertAsync(preference);
            if (!isSuccessful)
                return new BaseResponse<PropertyAlertPreferenceDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));

            await _unitOfWOrk.SaveAsync();

            var dto = new PropertyAlertPreferenceDto(
                preference.Id, preference.DateCreated, preference.PropertyType, preference.MinPrice,
                preference.MaxPrice, preference.City, preference.State, preference.Features, preference.IsActive);

            return new BaseResponse<PropertyAlertPreferenceDto>(dto, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreateAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyAlertPreferenceDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> DeleteAsync(Guid preferenceId, Guid customerId)
    {
        try
        {
            var preference = await _unitOfWOrk.PropertyAlertPreferenceQueries.GetByIdAsync(preferenceId);
            if (preference == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (preference.CustomerId != customerId)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.PropertyNotOwnedByUser);

            await _unitOfWOrk.PropertyAlertPreferenceCommands.DeleteAsync(preference);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.SetDeletedSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in DeleteAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }
}
