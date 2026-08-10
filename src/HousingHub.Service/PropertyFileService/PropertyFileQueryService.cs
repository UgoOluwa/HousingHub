using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyFile;
using HousingHub.Service.PropertyFileService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyFileService;

public class PropertyFileQueryService : IPropertyFileQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly ILogger<PropertyFileQueryService> _logger;
    private const string ClassName = "property file";

    public PropertyFileQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<PropertyFileQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<PropertyFileDto?>> GetPropertyFileAsync(Guid id)
    {
        try
        {
            PropertyFile? property = await _unitOfWOrk.PropertyFileQueries.GetByAsync(x => x.Id == id);
            if (property is null)
            {
                return new BaseResponse<PropertyFileDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<PropertyFileDto?>(_mapper.Map<PropertyFileDto>(property), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyFileDto?>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <param name="requestingUserId">
    /// Caller id, or null when anonymous. Photos of unpublished (draft or
    /// admin-unpublished) listings are visible only to the owner, matching how
    /// PropertyQueryService.GetPropertyAsync treats the listing itself.
    /// </param>
    public async Task<BaseResponse<List<PropertyFileDto>>> GetAllPropertyFilesAsync(Guid propertyId, Guid? requestingUserId = null)
    {
        try
        {
            var property = await _unitOfWOrk.PropertyQueries.GetByIdAsync(propertyId);
            bool visible = property is not null
                && (property.IsPublished
                    || (requestingUserId is not null && property.OwnerId == requestingUserId.Value));

            if (!visible)
            {
                return new BaseResponse<List<PropertyFileDto>>(
                    new List<PropertyFileDto>(), false, string.Empty,
                    ResponseMessages.SetNotFoundMessage(ClassName));
            }

            var properties = await _unitOfWOrk.PropertyFileQueries.GetAllAsync(x => x.PropertyId == propertyId);
            if (!properties.Any())
            {
                return new BaseResponse<List<PropertyFileDto>>(new List<PropertyFileDto>(), false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<List<PropertyFileDto>>(_mapper.Map<List<PropertyFileDto>>(properties), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyFileDto>>(new List<PropertyFileDto>(), false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }
}
