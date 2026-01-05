using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyAddress;
using HousingHub.Service.PropertyAddressService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyAddressService;

public class PropertyAddressCommandService : IPropertyAddressCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyAddressCommandService> _logger;
    private readonly IMapper _mapper;
    private const string ClassName = "property address";

    public PropertyAddressCommandService(ILogger<PropertyAddressCommandService> logger, IUnitOfWOrk unitOfWOrk, IMapper mapper)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PropertyAddressDto>> CreatePropertyAddress(CreatePropertyAddressDto request)
    {
        try
        {
            // Check for existing property address could be added here
            var existingAddress = await _unitOfWOrk.PropertyAddressQueries.AnyAsync(x => x.PropertyId == request.PropertyId);
            if (existingAddress)
            {
                return new BaseResponse<PropertyAddressDto>(null, false, string.Empty, ResponseMessages.SetAlreadyExistsMessage(ClassName));
            }

            var newEntity = _mapper.Map<PropertyAddress>(request);
            bool isSuccessful = await _unitOfWOrk.PropertyAddressCommands.InsertAsync(newEntity);
            if (!isSuccessful)
            {
                return new BaseResponse<PropertyAddressDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));
            }

            await _unitOfWOrk.SaveAsync();
            PropertyAddressDto response = _mapper.Map<PropertyAddressDto>(newEntity);
            return new BaseResponse<PropertyAddressDto>(response, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreatePropertyAddress: {Message}", ex.Message);
            return new BaseResponse<PropertyAddressDto>(null, false, string.Empty, ex.Message);
        }
    }
}
