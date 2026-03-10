using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.InspectionService;

public class InspectionQueryService : IInspectionQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly ILogger<InspectionQueryService> _logger;
    private const string ClassName = "inspection";

    public InspectionQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<InspectionQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<InspectionDto?>> GetInspectionAsync(Guid id)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(
                x => x.Id == id,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (inspection is null)
                return new BaseResponse<InspectionDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            return new BaseResponse<InspectionDto?>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto?>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<InspectionDto>>> GetInspectionsByPropertyAsync(Guid propertyId, int pageNumber, int pageSize, InspectionStatus? status = null)
    {
        try
        {
            System.Linq.Expressions.Expression<Func<PropertyInspection, bool>> predicate = status.HasValue
                ? x => x.PropertyId == propertyId && x.Status == status.Value
                : x => x.PropertyId == propertyId;

            var (inspections, totalCount) = await _unitOfWOrk.PropertyInspectionQueries.GetPagedAsync(
                pageNumber, pageSize,
                predicate: predicate,
                findOptions: new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            var mappedItems = _mapper.Map<List<InspectionDto>>(inspections);
            var paginatedResult = new PaginatedResult<InspectionDto>(mappedItems, totalCount, pageNumber, pageSize);

            return new BaseResponse<PaginatedResult<InspectionDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionsByPropertyAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<InspectionDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<InspectionDto>>> GetInspectionsByCustomerAsync(Guid customerId, int pageNumber, int pageSize, InspectionStatus? status = null)
    {
        try
        {
            System.Linq.Expressions.Expression<Func<PropertyInspection, bool>> predicate = status.HasValue
                ? x => x.CustomerId == customerId && x.Status == status.Value
                : x => x.CustomerId == customerId;

            var (inspections, totalCount) = await _unitOfWOrk.PropertyInspectionQueries.GetPagedAsync(
                pageNumber, pageSize,
                predicate: predicate,
                findOptions: new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            var mappedItems = _mapper.Map<List<InspectionDto>>(inspections);
            var paginatedResult = new PaginatedResult<InspectionDto>(mappedItems, totalCount, pageNumber, pageSize);

            return new BaseResponse<PaginatedResult<InspectionDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionsByCustomerAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<InspectionDto>>(null, false, string.Empty, ex.Message);
        }
    }
}
