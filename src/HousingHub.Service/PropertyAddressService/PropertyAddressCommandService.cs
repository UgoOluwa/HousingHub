using HousingHub.Service.Commons.Mappings;
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

    /// <summary>
    /// Attaches an address to a property.
    /// </summary>
    /// <param name="requestingUserId">
    /// The caller, taken from their token. Must own the target property — PropertyId
    /// arrives in the request body, so without this check any owner or agent could
    /// attach a fabricated address to someone else's listing.
    /// </param>
    public async Task<BaseResponse<PropertyAddressDto>> CreatePropertyAddress(CreatePropertyAddressDto request, Guid requestingUserId)
    {
        try
        {
            var property = await _unitOfWOrk.PropertyQueries.GetByIdAsync(request.PropertyId);
            if (property is null || property.OwnerId != requestingUserId)
            {
                _logger.LogWarning(
                    "Rejected address creation on property {PropertyId} by user {UserId}",
                    request.PropertyId, requestingUserId);
                return new BaseResponse<PropertyAddressDto>(
                    null, false, string.Empty, ResponseMessages.SetNotFoundMessage("property"));
            }

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
            return new BaseResponse<PropertyAddressDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }
}
