using AutoMapper;
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
    private const string ClassName = "property";

    public PropertyAddressQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<PropertyAddressQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<PropertyAddressDto?>> GetPropertyAddressAsync(Guid id)
    {
        try
        {
            PropertyAddress? propertyAddress = await _unitOfWOrk.PropertyAddressQueries.GetByAsync(x => x.Id == id);
            if (propertyAddress is null)
            {
                return new BaseResponse<PropertyAddressDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<PropertyAddressDto?>(_mapper.Map<PropertyAddressDto>(propertyAddress), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyAddressAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyAddressDto?>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PropertyAddressDto?>> GetPropertyAddressByPropertyIdAsync(Guid propertyId)
    {
        try
        {
            PropertyAddress? propertyAddress = await _unitOfWOrk.PropertyAddressQueries.GetByAsync(x => x.PropertyId == propertyId);
            if (propertyAddress is null)
            {
                return new BaseResponse<PropertyAddressDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<PropertyAddressDto?>(_mapper.Map<PropertyAddressDto>(propertyAddress), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyAddressByPropertyIdAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyAddressDto?>(null, false, string.Empty, ex.Message);
        }
    }


}
