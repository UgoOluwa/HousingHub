using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
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
                x => x.Id == id);

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
                predicate: predicate);

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
                predicate: predicate);

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

    public async Task<BaseResponse<PaginatedResult<OwnerInspectionDto>>> GetInspectionsByOwnerAsync(Guid ownerId, int pageNumber, int pageSize, InspectionStatus? status = null)
    {
        try
        {
            // 1. Get all owner property IDs (lightweight lookup)
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync(p => p.OwnerId == ownerId);
            var propertyIds = properties.Select(p => p.Id).ToHashSet();

            if (propertyIds.Count == 0)
            {
                var emptyResult = new PaginatedResult<OwnerInspectionDto>(new List<OwnerInspectionDto>(), 0, pageNumber, pageSize);
                return new BaseResponse<PaginatedResult<OwnerInspectionDto>>(emptyResult, true, string.Empty, ResponseMessages.Successful);
            }

            // 2. Get inspections filtered by owner's properties and optional status
            var inspections = status.HasValue
                ? await _unitOfWOrk.PropertyInspectionQueries.GetAllAsync(
                    i => propertyIds.Contains(i.PropertyId) && i.Status == status.Value)
                : await _unitOfWOrk.PropertyInspectionQueries.GetAllAsync(
                    i => propertyIds.Contains(i.PropertyId));

            var ordered = inspections
                .OrderByDescending(i => i.DateCreated)
                .ToList();

            var totalCount = ordered.Count;

            // 3. Paginate first, then fetch only the properties needed for this page
            var pagedInspections = ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedPropertyIds = pagedInspections.Select(i => i.PropertyId).ToHashSet();
            var propertyMap = properties
                .Where(p => pagedPropertyIds.Contains(p.Id))
                .ToDictionary(p => p.Id);

            var items = pagedInspections
                .Select(i =>
                {
                    var property = propertyMap[i.PropertyId];
                    return new OwnerInspectionDto(
                        i.InspectionId,
                        property.Title,
                        property.Latitude,
                        property.Longitude,
                        i.ScheduledDate,
                        i.ScheduledTime,
                        i.DateCreated,
                        i.Status);
                })
                .ToList();

            var paginatedResult = new PaginatedResult<OwnerInspectionDto>(items, totalCount, pageNumber, pageSize);
            return new BaseResponse<PaginatedResult<OwnerInspectionDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionsByOwnerAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<OwnerInspectionDto>>(null, false, string.Empty, ex.Message);
        }
    }
}
