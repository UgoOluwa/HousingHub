using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyFile;
using HousingHub.Service.PropertyFileService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyFileService;

public class PropertyFileCommandService : IPropertyFileCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyFileCommandService> _logger;
    private readonly IMapper _mapper;
    private const string ClassName = "property file";

    public PropertyFileCommandService(ILogger<PropertyFileCommandService> logger, IUnitOfWOrk unitOfWOrk, IMapper mapper)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PropertyFileDto>> CreatePropertyFile(CreatePropertyFileDto request)
    {
        try
        {
            var newEntity = _mapper.Map<PropertyFile>(request);
            bool isSuccessful = await _unitOfWOrk.PropertyFileCommands.InsertAsync(newEntity);
            if (!isSuccessful)
            {
                return new BaseResponse<PropertyFileDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));
            }

            await _unitOfWOrk.SaveAsync();
            PropertyFileDto response = _mapper.Map<PropertyFileDto>(newEntity);
            return new BaseResponse<PropertyFileDto>(response, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreatePropertyFile: {Message}", ex.Message);
            return new BaseResponse<PropertyFileDto>(null, false, string.Empty, ex.Message);
        }
    }
}
