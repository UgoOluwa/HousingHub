using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyAddress;
using HousingHub.Service.PropertyAddressService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyAddressService;

public class PropertyAddressQueryService : IPropertyAddressQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly ILogger<PropertyAddressQueryService> _logger;
    private const string ClassName = "property address";

    public PropertyAddressQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<PropertyAddressQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    /// <param name="requestingUserId">
    /// Caller id, or null for an anonymous request. Unpublished listings are only
    /// visible to their owner.
    /// </param>
    public async Task<BaseResponse<PropertyAddressDto?>> GetPropertyAddressAsync(Guid id, Guid? requestingUserId = null, bool includeUnpublished = false)
    {
        try
        {
            PropertyAddress? propertyAddress = await _unitOfWOrk.PropertyAddressQueries.GetByAsync(x => x.Id == id);
            if (propertyAddress is null || !await IsVisibleToAsync(propertyAddress.PropertyId, requestingUserId, includeUnpublished))
            {
                return new BaseResponse<PropertyAddressDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<PropertyAddressDto?>(_mapper.Map<PropertyAddressDto>(propertyAddress), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyAddressAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyAddressDto?>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <param name="requestingUserId">
    /// Caller id, or null for an anonymous request. Unpublished listings are only
    /// visible to their owner.
    /// </param>
    public async Task<BaseResponse<PropertyAddressDto?>> GetPropertyAddressByPropertyIdAsync(Guid propertyId, Guid? requestingUserId = null, bool includeUnpublished = false)
    {
        try
        {
            PropertyAddress? propertyAddress = await _unitOfWOrk.PropertyAddressQueries.GetByAsync(x => x.PropertyId == propertyId);
            if (propertyAddress is null || !await IsVisibleToAsync(propertyId, requestingUserId, includeUnpublished))
            {
                return new BaseResponse<PropertyAddressDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<PropertyAddressDto?>(_mapper.Map<PropertyAddressDto>(propertyAddress), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyAddressByPropertyIdAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyAddressDto?>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// A property's address is public once the listing is published. Before that it
    /// is visible only to the owner, matching how PropertyQueryService.GetPropertyAsync
    /// already treats the listing itself.
    /// </summary>
    private async Task<bool> IsVisibleToAsync(Guid propertyId, Guid? requestingUserId, bool includeUnpublished)
    {
        var property = await _unitOfWOrk.PropertyQueries.GetByIdAsync(propertyId);
        if (property is null) return false;

        return includeUnpublished
            || property.IsPublished
            || (requestingUserId is not null && property.OwnerId == requestingUserId.Value);
    }
}
