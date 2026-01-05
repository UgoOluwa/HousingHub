using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyInterest;
using HousingHub.Service.PropertyInterestService.Interfaces;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyInterestService;

public class PropertyInterestQueryService : IPropertyInterestQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly ILogger<PropertyInterestQueryService> _logger;
    private const string ClassName = "property interest";

    public PropertyInterestQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<PropertyInterestQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<PropertyInterestDto?>> GetPropertyInterestAsync(Guid id)
    {
        try
        {
            PropertyInterest? propertyInterests = await _unitOfWOrk.PropertyInterestQueries.GetByAsync(x => x.Id == id, findOptions: new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });
            if (propertyInterests is null)
            {
                return new BaseResponse<PropertyInterestDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<PropertyInterestDto?>(_mapper.Map<PropertyInterestDto>(propertyInterests), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetPropertyInterestAsync: {Message}", ex.Message);
            return new BaseResponse<PropertyInterestDto?>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<PropertyInterestDto>>> GetAllPropertyInterestsAsync(Guid propertyId)
    {
        try
        {
            var propertyInterests = await _unitOfWOrk.PropertyInterestQueries.GetAllAsync(x => x.PropertyId == propertyId);
            if (!propertyInterests.Any())
            {
                return new BaseResponse<List<PropertyInterestDto>>(new List<PropertyInterestDto>(), false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<List<PropertyInterestDto>>(_mapper.Map<List<PropertyInterestDto>>(propertyInterests), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllPropertyInterestsAsync: {Message}", ex.Message);
            return new BaseResponse<List<PropertyInterestDto>>(new List<PropertyInterestDto>(), false, string.Empty, ex.Message);
        }
    }
}
