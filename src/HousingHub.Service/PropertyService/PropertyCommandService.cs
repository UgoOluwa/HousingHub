using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.PropertyService.Interfaces;
using HousingHub.Service.Dtos.Property;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyService;

public class PropertyCommandService : IPropertyCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyCommandService> _logger;
    private readonly IMapper _mapper;
    private const string ClassName = "property";

    public PropertyCommandService(ILogger<PropertyCommandService> logger, IUnitOfWOrk unitOfWOrk, IMapper mapper)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PropertyDto>> CreateProperty(CreatePropertyDto request)
    {
        try
        {
            var newEntity = _mapper.Map<Property>(request);
            bool isSuccessful = await _unitOfWOrk.PropertyCommands.InsertAsync(newEntity);
            if (!isSuccessful)
            {
                return new BaseResponse<PropertyDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));
            }

            await _unitOfWOrk.SaveAsync();
            PropertyDto response = _mapper.Map<PropertyDto>(newEntity);
            return new BaseResponse<PropertyDto>(response, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreateProperty: {Message}", ex.Message);
            return new BaseResponse<PropertyDto>(null, false, string.Empty, ex.Message);
        }
    }
}
