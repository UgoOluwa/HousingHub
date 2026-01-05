using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyInterest;
using HousingHub.Service.PropertyInterestService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyInterestService;

public class PropertyInterestCommandService : IPropertyInterestCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyInterestCommandService> _logger;
    private readonly IMapper _mapper;
    private const string ClassName = "property interest";

    public PropertyInterestCommandService(ILogger<PropertyInterestCommandService> logger, IUnitOfWOrk unitOfWOrk, IMapper mapper)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PropertyInterestDto>> CreatePropertyInterest(CreatePropertyInterestDto request)
    {
        try
        {
            var newEntity = _mapper.Map<PropertyInterest>(request);
            bool isSuccessful = await _unitOfWOrk.PropertyInterestCommands.InsertAsync(newEntity);
            if (!isSuccessful)
            {
                return new BaseResponse<PropertyInterestDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));
            }

            await _unitOfWOrk.SaveAsync();
            PropertyInterestDto response = _mapper.Map<PropertyInterestDto>(newEntity);
            return new BaseResponse<PropertyInterestDto>(response, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreateProperty: {Message}", ex.Message);
            return new BaseResponse<PropertyInterestDto>(null, false, string.Empty, ex.Message);
        }
    }
}
