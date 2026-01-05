using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyService;

public class PropertyQueryService : IPropertyQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly ILogger<PropertyQueryService> _logger;
    private const string ClassName = "property";

    public PropertyQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<PropertyQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<PropertyDto?>> GetPropertyAsync(Guid id)
    {
        try
        {
            Property? property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == id);
            if (property is null)
            {
                return new BaseResponse<PropertyDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<PropertyDto?>(_mapper.Map<PropertyDto>(property), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyDto?>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<PropertyDto>>> GetAllPropertiesAsync()
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync();
            if (!properties.Any())
            {
                return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<List<PropertyDto>>(_mapper.Map<List<PropertyDto>>(properties), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllPropertiesAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyDto>>(new List<PropertyDto>(), false, string.Empty, ex.Message);
        }
    }
}
