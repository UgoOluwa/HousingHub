using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyAlert;
using HousingHub.Service.PropertyAlertService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyAlertService;

public class PropertyAlertPreferenceQueryService : IPropertyAlertPreferenceQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyAlertPreferenceQueryService> _logger;

    public PropertyAlertPreferenceQueryService(IUnitOfWOrk unitOfWOrk, ILogger<PropertyAlertPreferenceQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _logger = logger;
    }

    public async Task<BaseResponse<List<PropertyAlertPreferenceDto>>> GetByCustomerAsync(Guid customerId)
    {
        try
        {
            var preferences = await _unitOfWOrk.PropertyAlertPreferenceQueries.GetAllAsync(p => p.CustomerId == customerId);

            var items = preferences
                .OrderByDescending(p => p.DateCreated)
                .Select(p => new PropertyAlertPreferenceDto(
                    p.Id, p.DateCreated, p.PropertyType, p.MinPrice, p.MaxPrice, p.City, p.State, p.Features, p.IsActive))
                .ToList();

            return new BaseResponse<List<PropertyAlertPreferenceDto>>(items, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetByCustomerAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyAlertPreferenceDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<List<PropertyAlertPreference>> GetAllActiveAsync()
    {
        var all = await _unitOfWOrk.PropertyAlertPreferenceQueries.GetAllAsync(p => p.IsActive);
        return all.ToList();
    }
}
